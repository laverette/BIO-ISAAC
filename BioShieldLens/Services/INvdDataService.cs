namespace BioShieldLens.Services;

public interface INvdDataService
{
    Task<int> FetchAndImportVulnerabilitiesAsync(int maxResults = 100, string? keywordFilter = null);
    Task<List<Models.Vulnerability>> SearchVulnerabilitiesAsync(string keyword);
}

