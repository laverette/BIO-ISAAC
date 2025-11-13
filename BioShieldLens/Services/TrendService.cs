using System.Text;
using BioShieldLens.Data;
using BioShieldLens.Models;
using Microsoft.EntityFrameworkCore;

namespace BioShieldLens.Services;

public class TrendService : ITrendService
{
    private readonly BioShieldDbContext _context;
    private readonly ILogger<TrendService> _logger;

    public TrendService(BioShieldDbContext context, ILogger<TrendService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Trend>> GetTrendsAsync()
    {
        return await _context.Trends
            .OrderByDescending(t => t.Month)
            .ThenBy(t => t.Category)
            .ToListAsync();
    }

    public async Task CalculateTrendsAsync()
    {
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var lastMonth = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM");

        // Calculate trends by sector
        var sectors = await _context.Vulnerabilities
            .Where(v => v.AffectedSector != null)
            .Select(v => v.AffectedSector!)
            .Distinct()
            .ToListAsync();

        foreach (var sector in sectors)
        {
            var currentCount = await _context.Vulnerabilities
                .CountAsync(v => v.AffectedSector == sector && 
                                v.DateDiscovered != null &&
                                v.DateDiscovered.Value.ToString("yyyy-MM") == currentMonth);

            var lastCount = await _context.Vulnerabilities
                .CountAsync(v => v.AffectedSector == sector && 
                                v.DateDiscovered != null &&
                                v.DateDiscovered.Value.ToString("yyyy-MM") == lastMonth);

            decimal? changePercentage = null;
            if (lastCount > 0)
            {
                changePercentage = ((currentCount - lastCount) / (decimal)lastCount) * 100;
            }
            else if (currentCount > 0)
            {
                changePercentage = 100;
            }

            var existingTrend = await _context.Trends
                .FirstOrDefaultAsync(t => t.Category == sector && t.Month == currentMonth);

            if (existingTrend != null)
            {
                existingTrend.ChangePercentage = changePercentage;
                existingTrend.Notes = $"{currentCount} vulnerabilities this month vs {lastCount} last month";
            }
            else
            {
                _context.Trends.Add(new Trend
                {
                    Category = sector,
                    Month = currentMonth,
                    ChangePercentage = changePercentage,
                    Notes = $"{currentCount} vulnerabilities this month vs {lastCount} last month"
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<string> GetTrendSummaryAsync()
    {
        var trends = await GetTrendsAsync();
        var recentTrends = trends.Where(t => t.Month == DateTime.UtcNow.ToString("yyyy-MM")).ToList();

        if (!recentTrends.Any())
        {
            return "No trend data available for the current month.";
        }

        var summary = new StringBuilder();
        foreach (var trend in recentTrends)
        {
            if (trend.ChangePercentage.HasValue && trend.ChangePercentage.Value > 0)
            {
                summary.AppendLine($"{trend.Category}: {trend.ChangePercentage.Value:F1}% increase. {trend.Notes}");
            }
            else if (trend.ChangePercentage.HasValue && trend.ChangePercentage.Value < 0)
            {
                summary.AppendLine($"{trend.Category}: {Math.Abs(trend.ChangePercentage.Value):F1}% decrease. {trend.Notes}");
            }
        }

        return summary.ToString();
    }
}

