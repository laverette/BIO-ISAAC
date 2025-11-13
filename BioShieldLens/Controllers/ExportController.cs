using Microsoft.AspNetCore.Mvc;
using BioShieldLens.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace BioShieldLens.Controllers;

public class ExportController : Controller
{
    private readonly IVulnerabilityService _vulnerabilityService;
    private readonly ITrendService _trendService;

    public ExportController(
        IVulnerabilityService vulnerabilityService,
        ITrendService trendService)
    {
        _vulnerabilityService = vulnerabilityService;
        _trendService = trendService;
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] string? priority)
    {
        var vulnerabilities = string.IsNullOrEmpty(priority)
            ? await _vulnerabilityService.GetAllVulnerabilitiesAsync()
            : await _vulnerabilityService.GetVulnerabilitiesByPriorityAsync(priority);

        var csv = new StringBuilder();
        csv.AppendLine("CVE ID,Description,Severity Score,Bio Impact,Human Impact,Urgency Level,Affected Sector,Date Discovered");

        foreach (var vuln in vulnerabilities)
        {
            csv.AppendLine($"{vuln.CveId},\"{vuln.Description.Replace("\"", "\"\"")}\",{vuln.SeverityScore},{vuln.BioImpactScore},{vuln.HumanImpactScore},{vuln.UrgencyLevel},{vuln.AffectedSector},{vuln.DateDiscovered:yyyy-MM-dd}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"vulnerabilities_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] string? priority)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var vulnerabilities = string.IsNullOrEmpty(priority)
            ? await _vulnerabilityService.GetAllVulnerabilitiesAsync()
            : await _vulnerabilityService.GetVulnerabilitiesByPriorityAsync(priority);

        var stats = await _vulnerabilityService.GetVulnerabilityStatsAsync();
        var trends = await _trendService.GetTrendsAsync();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);

                page.Header().Text("BioShield Lens - Vulnerability Report")
                    .FontSize(20)
                    .Bold()
                    .AlignCenter();

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    // Statistics
                    column.Item().Text("Summary Statistics")
                        .FontSize(16)
                        .Bold();
                    column.Item().Text($"Total Vulnerabilities: {stats["Total"]}");
                    column.Item().Text($"Critical to Act Now: {stats["Critical to Act Now"]}");
                    column.Item().Text($"Monitor: {stats["Monitor"]}");
                    column.Item().Text($"Low Priority: {stats["Low Priority"]}");

                    column.Spacing(10);

                    // Vulnerabilities
                    column.Item().Text("Vulnerabilities")
                        .FontSize(16)
                        .Bold();

                    foreach (var vuln in vulnerabilities.Take(50))
                    {
                        column.Item().PaddingBottom(5).Column(vulnColumn =>
                        {
                            vulnColumn.Item().Text($"{vuln.CveId} - {vuln.UrgencyLevel}")
                                .FontSize(12)
                                .Bold();
                            vulnColumn.Item().Text(vuln.Description)
                                .FontSize(10);
                            if (!string.IsNullOrEmpty(vuln.IntelNotes))
                            {
                                vulnColumn.Item().Text($"Intel: {vuln.IntelNotes}")
                                    .FontSize(9)
                                    .Italic();
                            }
                        });
                    }
                });

                page.Footer().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"));
                });
            });
        });

        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/pdf", $"vulnerabilities_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }
}

