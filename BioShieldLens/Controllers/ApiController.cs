using Microsoft.AspNetCore.Mvc;
using BioShieldLens.Services;

namespace BioShieldLens.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApiController : ControllerBase
{
    private readonly IVulnerabilityService _vulnerabilityService;
    private readonly ITrendService _trendService;

    public ApiController(
        IVulnerabilityService vulnerabilityService,
        ITrendService trendService)
    {
        _vulnerabilityService = vulnerabilityService;
        _trendService = trendService;
    }

    [HttpGet("vulnerabilities")]
    public async Task<IActionResult> GetVulnerabilities([FromQuery] string? priority)
    {
        var vulnerabilities = string.IsNullOrEmpty(priority)
            ? await _vulnerabilityService.GetAllVulnerabilitiesAsync()
            : await _vulnerabilityService.GetVulnerabilitiesByPriorityAsync(priority);

        return Ok(vulnerabilities);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _vulnerabilityService.GetVulnerabilityStatsAsync();
        return Ok(stats);
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends()
    {
        var trends = await _trendService.GetTrendsAsync();
        return Ok(trends);
    }
}

