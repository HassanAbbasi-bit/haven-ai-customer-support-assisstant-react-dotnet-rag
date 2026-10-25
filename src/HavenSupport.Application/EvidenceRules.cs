using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HavenSupport.Domain;

namespace HavenSupport.Application;

public static partial class EvidenceRules
{
    public const int ChunkLimit = 900;
    public static IReadOnlyList<KnowledgeChunk> Chunk(Guid versionId, string text, int? page)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var chunks = new List<KnowledgeChunk>();
        var offset = 0;
        while (offset < text.Length)
        {
            var length = Math.Min(ChunkLimit, text.Length - offset);
            var nextHeading = Heading().Match(text, offset + 1);
            if (nextHeading.Success && nextHeading.Index < offset + length)
                length = nextHeading.Index - offset;
            if (offset + length < text.Length)
            {
                var boundary = text.LastIndexOf('\n', offset + length - 1, length);
                if (boundary < offset + length / 2) boundary = text.LastIndexOf(' ', offset + length - 1, length);
                if (boundary >= offset + length / 2) length = boundary - offset + 1;
            }
            var part = text.Substring(offset, length).Trim();
            if (part.Length > 0)
            {
                var ordinal = chunks.Count;
                var identity = SHA256.HashData(Encoding.UTF8.GetBytes($"{versionId}:{page}:{ordinal}:{part}"));
                var source = SourceKey().Match(part);
                chunks.Add(new KnowledgeChunk
                {
                    Id = new Guid(identity.AsSpan(0, 16)),
                    VersionId = versionId,
                    Ordinal = ordinal,
                    SourceKey = source.Success ? source.Value : page.HasValue ? $"PAGE-{page}-{ordinal + 1}" : $"TXT-{ordinal + 1}",
                    Page = page,
                    LineStart = page.HasValue ? null : text[..offset].Count(c => c == '\n') + 1,
                    LineEnd = page.HasValue ? null : text[..(offset + length)].Count(c => c == '\n') + 1,
                    Text = part
                });
            }
            offset += length;
        }
        return chunks;
    }
    public static bool Supports(string claim, string quote, string passage)
    {
        if (string.IsNullOrWhiteSpace(claim) || claim.Length > 1200 || quote.Length < 10 || quote.Length > ChunkLimit || !passage.Contains(quote, StringComparison.Ordinal)) return false;
        var quotedNumbers = Numbers().Matches(quote).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        return Numbers().Matches(claim).All(number => quotedNumbers.Contains(number.Value));
    }
    public static string? ResolveQuote(string proposed, string passage)
    {
        if (string.IsNullOrWhiteSpace(proposed) || proposed.Length is < 10 or > ChunkLimit) return null;
        if (passage.Contains(proposed, StringComparison.Ordinal)) return proposed;
        // PDF line wrapping is formatting, not a change in the quoted words.
        // Return the original stored span, including its actual whitespace.
        var pattern = string.Join(@"\s+", Regex.Split(proposed.Trim(), @"\s+").Select(Regex.Escape));
        try
        {
            var match = Regex.Match(passage, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            return match.Success ? match.Value : null;
        }
        catch (RegexMatchTimeoutException) { return null; }
    }
    [GeneratedRegex(@"\b[A-Z]{2,8}-\d{2}\b")]
    private static partial Regex SourceKey();
    [GeneratedRegex(@"(?m)^#{1,6}\s+\S")]
    private static partial Regex Heading();
    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex Numbers();
}
