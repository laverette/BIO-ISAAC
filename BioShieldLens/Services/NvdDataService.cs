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

    // Keywords to identify bio-related vulnerabilities (EXPANDED for more coverage)
    private readonly string[] _bioKeywords = {
        // Healthcare & Medical
        "medical", "hospital", "healthcare", "health", "patient", "clinic", "clinical", 
        "doctor", "nurse", "surgical", "surgery", "emergency", "ambulance", "icu", "intensive care",
        "telemedicine", "telehealth", "medical device", "pacemaker", "defibrillator", "ventilator",
        "infusion pump", "x-ray", "mri", "ct scan", "imaging", "radiology", "cardiology",
        "pharmacy", "prescription", "medication", "drug", "vaccine", "immunization",
        
        // Biotech & Laboratory
        "biotech", "biotechnology", "laboratory", "lab", "lims", "lab information",
        "biomedical", "genetic", "genomic", "dna", "rna", "sequencing", "genome",
        "pcr", "diagnostic", "pathology", "microbiology", "virology", "bacteriology",
        "biobank", "biobanking", "specimen", "sample", "cell culture", "tissue",
        "biosafety", "biosecurity", "biohazard", "containment", "clean room",
        "research", "clinical trial", "pharmaceutical", "pharma", "biopharmaceutical",
        
        // Agriculture & Food
        "agriculture", "agricultural", "farming", "farm", "crop", "livestock", "animal",
        "food safety", "food", "nutrition", "dairy", "meat", "poultry", "fishery",
        "veterinary", "vet", "animal health", "plant", "seed", "fertilizer", "pesticide",
        "irrigation", "harvest", "greenhouse", "aquaculture", "cattle", "pig", "chicken",
        
        // Public Health & Safety
        "epidemiology", "epidemic", "pandemic", "outbreak", "infection", "contagious",
        "pathogen", "virus", "bacteria", "disease", "illness", "public health",
        "cdc", "who", "fda", "health department", "quarantine", "isolation",
        "environmental health", "water quality", "sanitation", "hygiene",
        
        // Medical Records & Systems
        "ehr", "emr", "electronic health", "medical record", "patient record",
        "hipaa", "phi", "patient data", "health information", "medical data",
        "hospital management", "hospital system", "healthcare system"
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
            // Use a broader date range to get more vulnerabilities (last 6 months)
            // This will capture more bio-related CVEs for the database
            var startDate = DateTime.UtcNow.AddMonths(-6).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var endDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            
            // Limit resultsPerPage to 200 (NVD API recommendation)
            // Use pagination for larger requests
            var pageSize = Math.Min(200, maxResults);
            var totalFetched = 0;
            var startIndex = 0;

            while (totalFetched < maxResults)
            {
                var remaining = maxResults - totalFetched;
                var currentPageSize = Math.Min(pageSize, remaining);
                
                var url = $"{apiUrl}?resultsPerPage={currentPageSize}&startIndex={startIndex}&pubStartDate={startDate}&pubEndDate={endDate}";
                
                if (!string.IsNullOrEmpty(keywordFilter))
                {
                    url += $"&keywordSearch={Uri.EscapeDataString(keywordFilter)}";
                }

                _logger.LogInformation($"Fetching vulnerabilities from NVD (page {startIndex / pageSize + 1}): {url}");

                var response = await httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"NVD API returned {response.StatusCode}: {errorContent}");
                    
                    // If 404 or other error on first request, try with smaller date range
                    if (startIndex == 0 && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Retrying with smaller date range (last 3 months)");
                        startDate = DateTime.UtcNow.AddMonths(-3).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                        url = $"{apiUrl}?resultsPerPage={currentPageSize}&startIndex={startIndex}&pubStartDate={startDate}&pubEndDate={endDate}";
                        if (!string.IsNullOrEmpty(keywordFilter))
                        {
                            url += $"&keywordSearch={Uri.EscapeDataString(keywordFilter)}";
                        }
                        response = await httpClient.GetAsync(url);
                    }
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"NVD API returned {response.StatusCode}. Try a smaller count or different date range.");
                    }
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var nvdResponse = JsonSerializer.Deserialize<NvdApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (nvdResponse?.Vulnerabilities == null || !nvdResponse.Vulnerabilities.Any())
                {
                    _logger.LogInformation("No more vulnerabilities found in NVD response");
                    break;
                }

                var pageImported = 0;
                foreach (var item in nvdResponse.Vulnerabilities)
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
                    pageImported++;
                }

                await _context.SaveChangesAsync();
                totalFetched += nvdResponse.Vulnerabilities.Count;
                startIndex += currentPageSize;

                _logger.LogInformation($"Imported {pageImported} vulnerabilities from this page (total: {importedCount})");

                // If we got fewer results than requested, we've reached the end
                if (nvdResponse.Vulnerabilities.Count < currentPageSize)
                {
                    break;
                }

                // Rate limiting: wait a bit between requests
                if (totalFetched < maxResults)
                {
                    await Task.Delay(1000); // 1 second delay between API calls
                }
            }

            _logger.LogInformation($"Total imported: {importedCount} vulnerabilities from NVD");

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

