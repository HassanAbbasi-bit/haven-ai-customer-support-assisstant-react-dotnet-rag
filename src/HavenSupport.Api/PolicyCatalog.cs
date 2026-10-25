using System.Reflection;
using System.Text.RegularExpressions;

namespace HavenSupport.Api;

// Read-only demonstration fixtures, not a replacement for the publication pipeline.
public sealed record PolicyPassage(string Id, string Heading, string Text);
public sealed record PolicyDocument(string Id, string Title, string Version, string EffectiveDate, IReadOnlyList<PolicyPassage> Passages);

public static partial class PolicyCatalog
{
    public static IReadOnlyList<PolicyDocument> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HavenSupport.PolicyFixtures.md")
            ?? throw new InvalidOperationException("Policy fixtures are missing.");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd().Replace("\r\n", "\n");
        var documents = new List<PolicyDocument>();
        foreach (var section in content.Split("\n## Document: ").Skip(1))
        {
            var title = section[..section.IndexOf('\n')].Trim();
            var passages = PassagePattern().Matches(section).Select(match => new PolicyPassage(
                match.Groups[1].Value, match.Groups[2].Value.Trim(), match.Groups[3].Value.Trim())).ToArray();
            if (passages.Length == 0) throw new InvalidOperationException("A policy has no passages.");
            documents.Add(new PolicyDocument(passages[0].Id.Split('-')[0].ToLowerInvariant(), title, "1.0", "2026-10-01", passages));
        }
        return documents;
    }

    [GeneratedRegex(@"### ([A-Z]+-\d+) — ([^\n]+)\n+([\s\S]*?)(?=\n### |\z)")]
    private static partial Regex PassagePattern();
}
