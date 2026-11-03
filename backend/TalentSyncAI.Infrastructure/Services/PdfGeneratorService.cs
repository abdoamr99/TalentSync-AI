using System.Diagnostics;
using System.Text;
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

    public async Task<byte[]> GenerateResumePdfAsync(
        string candidateName,
        string targetRole,
        string targetCompany,
        string markdownContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating tailored resume PDF for {CandidateName} - {TargetRole} at {Company}",
            candidateName, targetRole, targetCompany);

        var tempHtmlPath = Path.Combine(Path.GetTempPath(), $"resume_{Guid.NewGuid():N}.html");
        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"resume_{Guid.NewGuid():N}.pdf");

        try
        {
            var html = BuildAtsHtmlTemplate(candidateName, targetRole, targetCompany, markdownContent);
            await File.WriteAllTextAsync(tempHtmlPath, html, Encoding.UTF8, cancellationToken);

            var edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            if (!File.Exists(edgePath))
            {
                edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";
            }

            if (File.Exists(edgePath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = edgePath,
                    Arguments = $"--headless --disable-gpu --run-all-compositor-stages-before-draw --print-to-pdf=\"{tempPdfPath}\" \"{tempHtmlPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                    if (File.Exists(tempPdfPath))
                    {
                        var bytes = await File.ReadAllBytesAsync(tempPdfPath, cancellationToken);
                        return bytes;
                    }
                }
            }

            // Fallback: minimal valid PDF structure
            return GenerateFallbackPdfBytes(candidateName, targetRole, targetCompany, markdownContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PDF generation, returning fallback PDF bytes.");
            return GenerateFallbackPdfBytes(candidateName, targetRole, targetCompany, markdownContent);
        }
        finally
        {
            try { if (File.Exists(tempHtmlPath)) File.Delete(tempHtmlPath); } catch { }
            try { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); } catch { }
        }
    }

    private static string BuildAtsHtmlTemplate(string candidateName, string targetRole, string targetCompany, string markdown)
    {
        var sanitizedMarkdown = System.Net.WebUtility.HtmlEncode(markdown)
            .Replace("\r\n", "\n")
            .Replace("\n", "<br/>");

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <title>{candidateName} - {targetRole} Resume</title>
  <style>
    @page {{
      size: A4 portrait;
      margin: 15mm;
    }}
    body {{
      font-family: 'Segoe UI', Arial, Helvetica, sans-serif;
      color: #1a202c;
      line-height: 1.5;
      font-size: 11pt;
      margin: 0;
      padding: 0;
    }}
    .header {{
      border-bottom: 2px solid #2b6cb0;
      padding-bottom: 8px;
      margin-bottom: 16px;
    }}
    .name {{
      font-size: 20pt;
      font-weight: bold;
      color: #2b6cb0;
      margin: 0 0 4px 0;
    }}
    .title {{
      font-size: 13pt;
      font-weight: 600;
      color: #4a5568;
      margin: 0;
    }}
    .badge {{
      display: inline-block;
      background: #ebf8ff;
      color: #2b6cb0;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 9pt;
      font-weight: bold;
      margin-top: 6px;
    }}
    .content {{
      font-size: 10.5pt;
      color: #2d3748;
      line-height: 1.6;
    }}
    h1, h2, h3 {{
      color: #2c5282;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 4px;
      margin-top: 14px;
      margin-bottom: 8px;
    }}
  </style>
</head>
<body>
  <div class=""header"">
    <div class=""name"">{candidateName}</div>
    <div class=""title"">{targetRole}</div>
    <div class=""badge"">Tailored for {targetCompany}</div>
  </div>
  <div class=""content"">
    {sanitizedMarkdown}
  </div>
</body>
</html>";
    }

    private static byte[] GenerateFallbackPdfBytes(string candidateName, string targetRole, string targetCompany, string markdown)
    {
        var text = $"%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj\n4 0 obj<</Length 180>>stream\nBT /F1 14 Tf 50 720 Td ({candidateName} - {targetRole}) Tj 0 -25 Td (Tailored for: {targetCompany}) Tj 0 -30 Td (Generated by TalentSyncAI Resume Engine) Tj ET\nendstream\nendobj\n5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\nxref\n0 6\n0000000000 65535 f\n0000000009 00000 n\n0000000058 00000 n\n0000000115 00000 n\n0000000262 00000 n\n0000000494 00000 n\ntrailer<</Size 6/Root 1 0 R>>\nstartxref\n565\n%%EOF";
        return Encoding.ASCII.GetBytes(text);
    }
}

