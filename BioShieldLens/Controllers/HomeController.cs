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
            var stats = await _vulnerabilityService.GetVulnerabilityStatsAsync();
            var trends = await _trendService.GetTrendsAsync();

            ViewBag.Critical = critical;
            ViewBag.Monitor = monitor;
            ViewBag.LowPriority = lowPriority;
            ViewBag.Stats = stats;
            ViewBag.Trends = trends;

            // Generate intel notes for critical vulnerabilities
            if (critical.Any())
            {
                ViewBag.IntelNotes = await _aiService.GenerateIntelNotesAsync(critical.Take(10).ToList());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            // Set empty defaults so the page still renders
            ViewBag.Critical = new List<Vulnerability>();
            ViewBag.Monitor = new List<Vulnerability>();
            ViewBag.LowPriority = new List<Vulnerability>();
            ViewBag.Stats = new Dictionary<string, int> { { "Total", 0 }, { "Critical to Act Now", 0 }, { "Monitor", 0 }, { "Low Priority", 0 } };
            ViewBag.Trends = new List<Trend>();
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

