using HavenSupport.Infrastructure.Configuration;
using HavenSupport.Infrastructure.Diagnostics;
using HavenSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HavenSupport.UnitTests;

public sealed class FoundationTests
{
    [Fact]
    public void PdfExtractionPreservesPageBoundaries()
    {
        var pages = PdfProbe.Extract(PdfProbe.CreateFixture());
        Assert.Equal(2, pages.Count);
        Assert.Equal(1, pages[0].Page);
        Assert.Contains("RET-01", pages[0].Text);
        Assert.DoesNotContain("RET-02", pages[0].Text);
        Assert.Equal(2, pages[1].Page);
        Assert.Contains("RET-02", pages[1].Text);
    }

    [Theory]
    [InlineData("file:///private", 6334)]
    [InlineData("http://localhost:11434", 0)]
    [InlineData("http://localhost:11434", 65536)]
    public void ProviderSettingsRejectInvalidEndpoints(string url, int port)
    {
        Assert.False(new ProviderOptions { OllamaBaseUrl = url, QdrantGrpcPort = port }.IsValid());
    }

    [Fact]
    public void SqlMigrationIsPresentAndGeneratesSqlWithoutConnecting()
    {
        using var db = new SupportDbContextFactory().CreateDbContext([]);
        Assert.Contains(db.Database.GetMigrations(), migration => migration.EndsWith("SupportApplication", StringComparison.Ordinal));
        var script = db.Database.GenerateCreateScript();
        Assert.Contains("FoundationProbes", script);
        Assert.Contains("nvarchar(160)", script);
    }
}
