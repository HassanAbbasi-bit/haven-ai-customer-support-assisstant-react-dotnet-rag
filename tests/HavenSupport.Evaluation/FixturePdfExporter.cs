using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
namespace HavenSupport.Evaluation;

public static class FixturePdfExporter
{
    public static void Run()
    {
        var root = Path.Combine(Environment.CurrentDirectory, ".local", "demo-pdfs"); Directory.CreateDirectory(root);
        var source = File.ReadAllText("docs/fixtures/knowledge-base.md").Replace("\r\n", "\n");
        foreach (var section in source.Split("\n## Document: ").Skip(1))
        {
            var title = section[..section.IndexOf('\n')].Trim();
            var filename = string.Concat(title.Select(c => char.IsLetterOrDigit(c) ? c : '-')) + ".pdf";
            var path = Path.Combine(root, filename);
            if (File.Exists(path)) { Console.WriteLine($"Preserving fixture: {filename}"); continue; }
            var writer = new PdfDocumentBuilder(); var font = writer.AddStandard14Font(Standard14Font.Helvetica);
            var page = writer.AddPage(PageSize.A4); var y = 760;
            var lines = new List<string> { title, "Haven Goods - fictional demo policy - Version 1.0 - Effective 1 October 2026", "" };
            foreach (var paragraph in section[(section.IndexOf('\n') + 1)..].Replace('—', '-').Split('\n'))
            {
                var current = new StringBuilder();
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (current.Length + word.Length > 86) { lines.Add(current.ToString()); current.Clear(); }
                    if (current.Length > 0) current.Append(' '); current.Append(word);
                }
                lines.Add(current.ToString());
            }
            foreach (var line in lines)
            {
                if (y < 55) { page = writer.AddPage(PageSize.A4); y = 760; }
                if (line.Length > 0) page.AddText(line, 10, new PdfPoint(35, y), font);
                y -= 14;
            }
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write); output.Write(writer.Build());
            Console.WriteLine($"Created {filename}");
        }
    }
}
