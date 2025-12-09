using Microsoft.AspNetCore.Mvc;
using BioShieldLens.Services;
using BioShieldLens.Models;

namespace BioShieldLens.Controllers;

public class HomeController : Controller
{
    private readonly IVulnerabilityService _vulnerabilityService;
    private readonly ITrendService _trendService;
    private readonly IAiClassificationService _aiService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IVulnerabilityService vulnerabilityService,
        ITrendService trendService,
        IAiClassificationService aiService,
        ILogger<HomeController> logger)
    {
        _vulnerabilityService = vulnerabilityService;
        _trendService = trendService;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var critical = await _vulnerabilityService.GetVulnerabilitiesByPriorityAsync("Critical to Act Now");
            var monitor = await _vulnerabilityService.GetVulnerabilitiesByPriorityAsync("Monitor");
            var lowPriority = await _vulnerabilityService.GetVulnerabilitiesByPriorityAsync("Low Priority");
            var allVulnerabilities = await _vulnerabilityService.GetAllVulnerabilitiesAsync();
            var stats = await _vulnerabilityService.GetVulnerabilityStatsAsync();
            var trends = await _trendService.GetTrendsAsync();
            
            // Calculate sector distribution for the chart
            var sectorDistribution = allVulnerabilities
                .Where(v => !string.IsNullOrEmpty(v.AffectedSector))
                .GroupBy(v => v.AffectedSector!)
                .Select(g => new { Sector = g.Key ?? "Unknown", Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            // Calculate counts for the 4 main vulnerability types (sectors)
            var sectorCounts = new Dictionary<string, int>
            {
                ["Healthcare"] = allVulnerabilities.Count(v => v.AffectedSector == "Healthcare"),
                ["Biotech"] = allVulnerabilities.Count(v => v.AffectedSector == "Biotech"),
                ["Agriculture"] = allVulnerabilities.Count(v => v.AffectedSector == "Agriculture"),
                ["General"] = allVulnerabilities.Count(v => string.IsNullOrEmpty(v.AffectedSector) || v.AffectedSector == "General")
            };

            ViewBag.Critical = critical;
            ViewBag.Monitor = monitor;
            ViewBag.LowPriority = lowPriority;
            ViewBag.AllVulnerabilities = allVulnerabilities;
            ViewBag.Stats = stats;
            ViewBag.Trends = trends;
            ViewBag.SectorDistribution = sectorDistribution;
            ViewBag.SectorCounts = sectorCounts;

            // Generate intel notes for all critical vulnerabilities (but AI will use sample for analysis)
            if (critical.Any())
            {
                ViewBag.IntelNotes = await _aiService.GenerateIntelNotesAsync(critical);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            // Set empty defaults so the page still renders
            ViewBag.Critical = new List<Vulnerability>();
            ViewBag.Monitor = new List<Vulnerability>();
            ViewBag.LowPriority = new List<Vulnerability>();
            ViewBag.AllVulnerabilities = new List<Vulnerability>();
            ViewBag.Stats = new Dictionary<string, int> { { "Total", 0 }, { "Critical to Act Now", 0 }, { "Monitor", 0 }, { "Low Priority", 0 } };
            ViewBag.Trends = new List<Trend>();
            ViewBag.SectorDistribution = new List<object>();
            ViewBag.SectorCounts = new Dictionary<string, int> { { "Healthcare", 0 }, { "Biotech", 0 }, { "Agriculture", 0 }, { "General", 0 } };
            ViewBag.IntelNotes = "Unable to load data. Please check database connection.";
            TempData["Error"] = "Database connection failed. The page will show empty data. Please check your connection string.";
        }

        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}

