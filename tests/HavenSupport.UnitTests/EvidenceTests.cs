using HavenSupport.Application;
using HavenSupport.Domain;
using HavenSupport.Infrastructure;
namespace HavenSupport.UnitTests;

public sealed class EvidenceTests
{
    [Fact]
    public void ChunkingPreservesNegationsAndHasStableBoundedIdentifiers()
    {
        var id = Guid.NewGuid(); var text = "Opened products cannot be returned for change of mind.\n\n" + new string('x', 2000);
        var first = EvidenceRules.Chunk(id, text, null); var second = EvidenceRules.Chunk(id, text, null);
        Assert.Equal(first.Select(c => c.Id), second.Select(c => c.Id));
        Assert.All(first, chunk => Assert.InRange(chunk.Text.Length, 1, EvidenceRules.ChunkLimit));
        Assert.Contains(first, chunk => chunk.Text.Contains("cannot be returned", StringComparison.Ordinal));
    }
    [Fact]
    public void CitationValidationRejectsFabricatedQuotesAndNumbers()
    {
        const string passage = "Unopened products may be returned within 30 calendar days after delivery.";
        Assert.True(EvidenceRules.Supports("The return window is 30 calendar days.", passage, passage));
        Assert.False(EvidenceRules.Supports("The return window is 90 days.", passage, passage));
        Assert.False(EvidenceRules.Supports("Returns are free.", "All returns are free.", passage));
        Assert.False(EvidenceRules.Supports("Returns are free.", "", passage));
    }
    [Fact]
    public void ExtractorRejectsEmptyTextInsteadOfIndexingAnEmptyDocument()
    {
        var error = Assert.Throws<ProviderFailure>(() => RagEngine.Extract(new DocumentVersion { FileType = "txt" }, []));
        Assert.Equal("empty_document", error.Code);
    }
    [Fact]
    public void PdfChunksPreserveActualPageNumbers()
    {
        var chunks = RagEngine.Extract(new DocumentVersion { FileType = "pdf" }, HavenSupport.Infrastructure.Diagnostics.PdfProbe.CreateFixture());
        Assert.Contains(chunks, c => c.Page == 1 && c.Text.Contains("RET-01", StringComparison.Ordinal));
        Assert.Contains(chunks, c => c.Page == 2 && c.Text.Contains("RET-02", StringComparison.Ordinal));
        Assert.All(chunks, c => Assert.Null(c.LineStart));
    }

    [Theory]
    [InlineData("S9", "Returns are permitted within 30 days.", "Returns are permitted within 30 days.")]
    [InlineData("S1", "Returns are permitted within 90 days.", "Returns are permitted within 30 days.")]
    [InlineData("S1", "Returns are always free.", "All returns are free.")]
    public void ModelCannotSupplyItsOwnPolicyWording(string sourceId, string claim, string quote)
    {
        var chunk = new KnowledgeChunk { Id = Guid.NewGuid(), Text = "Returns are permitted within 30 days." };
        var raw = System.Text.Json.JsonSerializer.Serialize(new { kind = "answered", question = "", claims = new[] { new { text = claim, sourceId, quote } } });
        Assert.Throws<ProviderFailure>(() => RagEngine.ValidateAnswer(raw, [chunk]));
    }
    [Fact]
    public void ModelOutputCannotSmuggleExtraFieldsOrNullClaims()
    {
        Assert.Throws<ProviderFailure>(() => RagEngine.ValidateAnswer("{\"kind\":\"not_found\",\"claims\":[],\"secret\":\"hidden\"}", []));
        Assert.Throws<ProviderFailure>(() => RagEngine.ValidateAnswer("{\"kind\":\"answered\",\"claims\":[null]}", []));
        Assert.Throws<ProviderFailure>(() => RagEngine.ValidateAnswer("not json", []));
    }
    [Fact]
    public void MarkdownPassageBoundariesKeepTheirOwnStableSourceKeys()
    {
        var chunks = EvidenceRules.Chunk(Guid.NewGuid(), "### RET-01 - First rule\n\nUnopened products: 30 days.\n\n### RET-02 - Second rule\n\nOpened products cannot be returned.\n", null);
        Assert.Equal(new[] { "RET-01", "RET-02" }, chunks.Select(c => c.SourceKey));
        Assert.DoesNotContain("RET-02", chunks[0].Text);
        Assert.Contains("cannot be returned", chunks[1].Text);
    }
    [Fact]
    public void PdfWrappingResolvesToTheExactStoredQuoteWithoutChangingWords()
    {
        const string stored = "Opened products cannot be returned for\nchange of mind. Defects need review.";
        var quote = EvidenceRules.ResolveQuote("Opened products cannot be returned for change of mind.", stored);
        Assert.Equal("Opened products cannot be returned for\nchange of mind.", quote);
        Assert.Null(EvidenceRules.ResolveQuote("Opened products can be returned for change of mind.", stored));
    }
    [Fact]
    public void SelectedPolicyKeepsItsNegationAndExceptionInsteadOfAnInventedDecision()
    {
        var chunk = new KnowledgeChunk { Id = Guid.NewGuid(), Text = "### RET-02 - Opened products\nOpened products cannot be returned for\nchange of mind. Defective products need review within seven days." };
        var answer = RagEngine.ValidateAnswer("{\"sourceIds\":[\"S1\"]}", [chunk]);
        var claim = Assert.Single(answer.Claims);
        Assert.StartsWith("Opened products cannot", claim.Text);
        Assert.Contains("within seven days", claim.Text);
        Assert.Contains(claim.Quote, chunk.Text);
        Assert.Equal(chunk.Id.ToString(), claim.SourceId);
        Assert.Throws<ProviderFailure>(() => RagEngine.ValidateAnswer("{\"sourceIds\":[\"S9\"]}", [chunk]));
    }
}
