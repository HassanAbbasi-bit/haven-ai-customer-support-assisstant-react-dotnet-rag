using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace HavenSupport.Infrastructure.Diagnostics;

public static class PdfProbe
{
    public static byte[] CreateFixture()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("RET-01: Unopened items: 30 days.", 12, new PdfPoint(25, 700), font);
        builder.AddPage(PageSize.A4).AddText("RET-02: Opened items: no change-of-mind returns.", 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }

    public static IReadOnlyList<(int Page, string Text)> Extract(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        return document.GetPages()
            .Select(page => (page.Number, ContentOrderTextExtractor.GetText(page)))
            .ToArray();
    }

    public static string Run()
    {
        var pages = Extract(CreateFixture());
        if (pages.Count != 2 || pages[0].Page != 1 || pages[1].Page != 2
            || !pages[0].Text.Contains("RET-01", StringComparison.Ordinal)
            || !pages[1].Text.Contains("RET-02", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PDF page/text provenance check failed.");
        }
        return "Two-page text PDF generated and extracted; source keys preserved on pages 1 and 2.";
    }
}
