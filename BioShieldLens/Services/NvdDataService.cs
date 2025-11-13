using System.Text.Json;
using BioShieldLens.Data;
using BioShieldLens.Models;
using Microsoft.EntityFrameworkCore;

namespace BioShieldLens.Services;

public class NvdDataService : INvdDataService
{
    private readonly BioShieldDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NvdDataService> _logger;
    private readonly IConfiguration _configuration;

    // Keywords to identify bio-related vulnerabilities
    private readonly string[] _bioKeywords = {
        "medical", "hospital", "healthcare", "biotech", "biotechnology",
        "laboratory", "lab", "pharmaceutical", "pharma", "clinical",
        "diagnostic", "biomedical", "genetic", "dna", "rna", "sequencing",
        "agriculture", "farming", "crop", "livestock", "food safety",
        "epidemiology", "pathogen", "biosafety", "biosecurity"
    };

    public NvdDataService(
        BioShieldDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<NvdDataService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<int> FetchAndImportVulnerabilitiesAsync(int maxResults = 100, string? keywordFilter = null)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "BioShieldLens/1.0");

        var importedCount = 0;
        var apiUrl = _configuration["Nvd:ApiUrl"] ?? "https://services.nvd.nist.gov/rest/json/cves/2.0";

        try
        {
            // Fetch recent CVEs
            var url = $"{apiUrl}?resultsPerPage={Math.Min(maxResults, 2000)}&pubStartDate={DateTime.UtcNow.AddDays(-30):yyyy-MM-ddTHH:mm:ss:fff UTC-05:00}";
            
            if (!string.IsNullOrEmpty(keywordFilter))
            {
                url += $"&keywordSearch={Uri.EscapeDataString(keywordFilter)}";
            }

            _logger.LogInformation($"Fetching vulnerabilities from NVD: {url}");

            var response = await httpClient.GetStringAsync(url);
            var nvdResponse = JsonSerializer.Deserialize<NvdApiResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (nvdResponse?.Vulnerabilities == null)
            {
                _logger.LogWarning("No vulnerabilities found in NVD response");
                return 0;
            }

            foreach (var item in nvdResponse.Vulnerabilities.Take(maxResults))
            {
                var cve = item.Cve;
                if (cve == null) continue;

                // Check if already exists
                var exists = await _context.Vulnerabilities
                    .AnyAsync(v => v.CveId == cve.Id);

                if (exists) continue;

                // Extract description
                var description = cve.Descriptions?
                    .FirstOrDefault(d => d.Lang == "en")?.Value ?? "No description available";

                // Check if bio-related
                var isBioRelated = _bioKeywords.Any(keyword =>
                    description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (cve.Id != null && cve.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

                // Only import bio-related or if keyword filter is specified
                if (!isBioRelated && string.IsNullOrEmpty(keywordFilter))
                {
                    continue;
                }

                // Extract CVSS score if available
                decimal? cvssScore = null;
                if (cve.Metrics?.CvssMetricV31 != null && cve.Metrics.CvssMetricV31.Any())
                {
                    var metric = cve.Metrics.CvssMetricV31[0];
                    if (metric?.CvssData != null)
                    {
                        cvssScore = (decimal)metric.CvssData.BaseScore;
                    }
                }
                else if (cve.Metrics?.CvssMetricV30 != null && cve.Metrics.CvssMetricV30.Any())
                {
                    var metric = cve.Metrics.CvssMetricV30[0];
                    if (metric?.CvssData != null)
                    {
                        cvssScore = (decimal)metric.CvssData.BaseScore;
                    }
                }
                else if (cve.Metrics?.CvssMetricV2 != null && cve.Metrics.CvssMetricV2.Any())
                {
                    var metric = cve.Metrics.CvssMetricV2[0];
                    if (metric?.CvssData != null)
                    {
                        cvssScore = (decimal)metric.CvssData.BaseScore;
                    }
                }

                var vulnerability = new Vulnerability
                {
                    CveId = cve.Id ?? "UNKNOWN",
                    Description = description,
                    Source = "NVD",
                    SeverityScore = cvssScore,
                    DateDiscovered = cve.Published != null ? DateTime.Parse(cve.Published) : DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Vulnerabilities.Add(vulnerability);
                importedCount++;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Imported {importedCount} vulnerabilities from NVD");

            return importedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vulnerabilities from NVD");
            throw;
        }
    }

    public async Task<List<Vulnerability>> SearchVulnerabilitiesAsync(string keyword)
    {
        return await _context.Vulnerabilities
            .Where(v => v.Description.Contains(keyword) || v.CveId.Contains(keyword))
            .OrderByDescending(v => v.DateDiscovered)
            .ToListAsync();
    }

    // NVD API Response Models
    private class NvdApiResponse
    {
        public List<NvdVulnerabilityItem>? Vulnerabilities { get; set; }
    }

    private class NvdVulnerabilityItem
    {
        public NvdCve? Cve { get; set; }
    }

    private class NvdCve
    {
        public string? Id { get; set; }
        public List<NvdDescription>? Descriptions { get; set; }
        public string? Published { get; set; }
        public NvdMetrics? Metrics { get; set; }
    }

    private class NvdDescription
    {
        public string? Lang { get; set; }
        public string? Value { get; set; }
    }

    private class NvdMetrics
    {
        public List<NvdCvssMetric>? CvssMetricV31 { get; set; }
        public List<NvdCvssMetric>? CvssMetricV30 { get; set; }
        public List<NvdCvssMetric>? CvssMetricV2 { get; set; }
    }

    private class NvdCvssMetric
    {
        public NvdCvssData? CvssData { get; set; }
    }

    private class NvdCvssData
    {
        public double BaseScore { get; set; }
    }
}

