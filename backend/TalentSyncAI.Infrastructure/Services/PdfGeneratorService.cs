using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TalentSyncAI.Application.Common.Interfaces;

namespace TalentSyncAI.Infrastructure.Services;

public class PdfGeneratorService : IPdfGeneratorService
{
    private readonly ILogger<PdfGeneratorService> _logger;

    public PdfGeneratorService(ILogger<PdfGeneratorService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> GenerateResumePdfAsync(
        string candidateName,
        string targetRole,
        string targetCompany,
        string markdownContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating tailored resume PDF for {CandidateName} - {TargetRole} at {Company}",
            candidateName, targetRole, targetCompany);

        var bytes = BuildAtsPdfDocument(candidateName, targetRole, targetCompany, markdownContent);
        return Task.FromResult(bytes);
    }

    private class PdfPageData
    {
        public StringBuilder Content { get; } = new();
    }

    private static byte[] BuildAtsPdfDocument(
        string candidateName,
        string targetRole,
        string targetCompany,
        string markdownContent)
    {
        const float leftMargin = 45f;
        const float rightMargin = 550f;
        const float topMargin = 780f;
        const float bottomMargin = 52f;

        var pages = new List<PdfPageData>();
        var curPage = new PdfPageData();
        pages.Add(curPage);
        float curY = topMargin;

        void EnsureSpace(float needed)
        {
            if (curY - needed < bottomMargin)
            {
                curPage = new PdfPageData();
                pages.Add(curPage);
                curY = topMargin;
            }
        }

        // --- PAGE 1 HEADER ---
        // Candidate Name (Bold 20pt Deep Navy)
        curPage.Content.AppendLine("0.11 0.33 0.58 rg");
        curPage.Content.AppendLine($"BT /F2 20 Tf {leftMargin:F1} {curY:F1} Td ({EscapePdf(candidateName)}) Tj ET");
        curY -= 22f;

        // Target Role (Bold 12pt Charcoal)
        curPage.Content.AppendLine("0.25 0.30 0.38 rg");
        curPage.Content.AppendLine($"BT /F2 12 Tf {leftMargin:F1} {curY:F1} Td ({EscapePdf(targetRole)}) Tj ET");
        curY -= 15f;

        // Target Organization & Metadata (9.5pt Blue)
        curPage.Content.AppendLine("0.15 0.45 0.70 rg");
        curPage.Content.AppendLine($"BT /F1 9.5 Tf {leftMargin:F1} {curY:F1} Td (Tailored for: {EscapePdf(targetCompany)} | Powered by TalentSyncAI Resume Intelligence) Tj ET");
        curY -= 10f;

        // Divider Line
        curPage.Content.AppendLine($"0.20 0.42 0.68 RG 0.8 w {leftMargin:F1} {curY:F1} m {rightMargin:F1} {curY:F1} l S");
        curY -= 18f;

        // --- PARSE MARKDOWN CONTENT ---
        var lines = (markdownContent ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                curY -= 6f;
                continue;
            }

            // Skip main document title if it starts with # since candidateName is already in header
            if (line.StartsWith("# "))
            {
                continue;
            }

            // Horizontal rule
            if (line == "---")
            {
                EnsureSpace(15f);
                curPage.Content.AppendLine($"0.80 0.80 0.80 RG 0.4 w {leftMargin:F1} {curY:F1} m {rightMargin:F1} {curY:F1} l S");
                curY -= 12f;
                continue;
            }

            // Section Heading: ## Heading
            if (line.StartsWith("## "))
            {
                EnsureSpace(42f);
                var heading = CleanMarkdown(line[3..].Trim());
                curY -= 6f;

                // Section title text
                curPage.Content.AppendLine("0.11 0.33 0.58 rg");
                curPage.Content.AppendLine($"BT /F2 12 Tf {leftMargin:F1} {curY:F1} Td ({EscapePdf(heading)}) Tj ET");
                curY -= 4f;

                // Underline
                curPage.Content.AppendLine($"0.20 0.42 0.68 RG 0.5 w {leftMargin:F1} {curY:F1} m {rightMargin:F1} {curY:F1} l S");
                curY -= 14f;
                continue;
            }

            // Subsection Heading: ### Subheading
            if (line.StartsWith("### "))
            {
                EnsureSpace(26f);
                var subheading = CleanMarkdown(line[4..].Trim());
                curPage.Content.AppendLine("0.18 0.24 0.32 rg");
                curPage.Content.AppendLine($"BT /F2 10.5 Tf {leftMargin:F1} {curY:F1} Td ({EscapePdf(subheading)}) Tj ET");
                curY -= 14f;
                continue;
            }

            // Bullet Point: - item or * item
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                EnsureSpace(20f);
                var bulletContent = CleanMarkdown(line[2..].Trim());
                var wrapped = WrapText(bulletContent, 82);

                for (int i = 0; i < wrapped.Count; i++)
                {
                    EnsureSpace(13f);
                    curPage.Content.AppendLine("0.15 0.15 0.15 rg");
                    if (i == 0)
                    {
                        // Draw bullet point marker
                        curPage.Content.AppendLine($"BT /F2 10 Tf {(leftMargin + 6f):F1} {curY:F1} Td (-) Tj ET");
                    }
                    curPage.Content.AppendLine($"BT /F1 9.5 Tf {(leftMargin + 18f):F1} {curY:F1} Td ({EscapePdf(wrapped[i])}) Tj ET");
                    curY -= 13f;
                }
                curY -= 2f;
                continue;
            }

            // Normal paragraph line
            var cleanText = CleanMarkdown(line);
            var paraWrapped = WrapText(cleanText, 86);
            foreach (var pLine in paraWrapped)
            {
                EnsureSpace(13f);
                curPage.Content.AppendLine("0.15 0.15 0.15 rg");
                curPage.Content.AppendLine($"BT /F1 9.5 Tf {leftMargin:F1} {curY:F1} Td ({EscapePdf(pLine)}) Tj ET");
                curY -= 13f;
            }
            curY -= 3f;
        }

        // --- FOOTERS ON ALL PAGES ---
        int totalPages = pages.Count;
        for (int p = 0; p < totalPages; p++)
        {
            var page = pages[p];
            page.Content.AppendLine($"0.85 0.85 0.85 RG 0.3 w {leftMargin:F1} 35.0 m {rightMargin:F1} 35.0 l S");
            page.Content.AppendLine("0.50 0.50 0.50 rg");
            page.Content.AppendLine($"BT /F1 8 Tf {leftMargin:F1} 24.0 Td (TalentSyncAI ATS Resume Intelligence) Tj ET");
            page.Content.AppendLine($"BT /F1 8 Tf {(rightMargin - 60f):F1} 24.0 Td (Page {p + 1} of {totalPages}) Tj ET");
        }

        return CompilePdfBinary(pages);
    }

    private static string CleanMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var res = Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1");
        res = Regex.Replace(res, @"\*([^*]+)\*", "$1");
        return res.Replace("`", string.Empty);
    }

    private static string EscapePdf(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\u2014':
                case '\u2013': sb.Append("--"); break;
                case '\u2022': sb.Append("-"); break;
                case '\u201c':
                case '\u201d': sb.Append("\""); break;
                case '\u2018':
                case '\u2019': sb.Append("'"); break;
                case '\t': sb.Append(" "); break;
                default:
                    if (c >= 32 && c <= 126) sb.Append(c);
                    else sb.Append(' ');
                    break;
            }
        }
        return sb.ToString();
    }

    private static List<string> WrapText(string text, int maxChars)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
            }
            else if (current.Length + 1 + word.Length <= maxChars)
            {
                current.Append(' ').Append(word);
            }
            else
            {
                result.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        return result;
    }

    private static byte[] CompilePdfBinary(List<PdfPageData> pages)
    {
        int pageCount = pages.Count;
        int fontF1 = 2 + (2 * pageCount) + 1;
        int fontF2 = fontF1 + 1;
        int totalObjects = fontF2;

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var offsets = new List<long> { 0 };

        // 1 0 obj: Catalog
        offsets.Add(ms.Position);
        writer.Write("1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n");
        writer.Flush();

        // 2 0 obj: Pages
        var kids = new StringBuilder();
        for (int i = 0; i < pageCount; i++)
        {
            int pageObjNum = 3 + (i * 2);
            kids.Append(pageObjNum).Append(" 0 R ");
        }
        offsets.Add(ms.Position);
        writer.Write($"2 0 obj\n<</Type/Pages/Kids[{kids.ToString().TrimEnd()}]/Count {pageCount}>>\nendobj\n");
        writer.Flush();

        // Page and Contents objects
        for (int i = 0; i < pageCount; i++)
        {
            int pageObjNum = 3 + (i * 2);
            int contentObjNum = pageObjNum + 1;

            // Page Obj
            offsets.Add(ms.Position);
            writer.Write($"{pageObjNum} 0 obj\n<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents {contentObjNum} 0 R/Resources<</Font<</F1 {fontF1} 0 R/F2 {fontF2} 0 R>>>>>>\nendobj\n");
            writer.Flush();

            // Content Obj
            byte[] contentBytes = Encoding.ASCII.GetBytes(pages[i].Content.ToString());
            offsets.Add(ms.Position);
            writer.Write($"{contentObjNum} 0 obj\n<</Length {contentBytes.Length}>>\nstream\n");
            writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            writer.Write("\nendstream\nendobj\n");
            writer.Flush();
        }

        // Font F1 (Helvetica)
        offsets.Add(ms.Position);
        writer.Write($"{fontF1} 0 obj\n<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>\nendobj\n");
        writer.Flush();

        // Font F2 (Helvetica-Bold)
        offsets.Add(ms.Position);
        writer.Write($"{fontF2} 0 obj\n<</Type/Font/Subtype/Type1/BaseFont/Helvetica-Bold>>\nendobj\n");
        writer.Flush();

        // xref Table
        long xrefPos = ms.Position;
        writer.Write($"xref\n0 {totalObjects + 1}\n");
        writer.Write("0000000000 65535 f \n");
        for (int i = 1; i <= totalObjects; i++)
        {
            writer.Write($"{offsets[i]:D10} 00000 n \n");
        }
        writer.Write($"trailer\n<</Size {totalObjects + 1}/Root 1 0 R>>\nstartxref\n{xrefPos}\n%%EOF\n");
        writer.Flush();

        return ms.ToArray();
    }
}
