using BioShieldLens.Models;

namespace BioShieldLens.Services;

public interface ITrendService
{
    Task<List<Trend>> GetTrendsAsync();
    Task CalculateTrendsAsync();
    Task<string> GetTrendSummaryAsync();
}

