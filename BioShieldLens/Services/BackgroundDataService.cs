using Microsoft.EntityFrameworkCore;
using BioShieldLens.Data;

namespace BioShieldLens.Services;

public class BackgroundDataService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundDataService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromHours(6); // Fetch every 6 hours

    public BackgroundDataService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundDataService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit on startup before first run
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var nvdService = scope.ServiceProvider.GetRequiredService<INvdDataService>();
                var vulnerabilityService = scope.ServiceProvider.GetRequiredService<IVulnerabilityService>();
                var aiService = scope.ServiceProvider.GetRequiredService<IAiClassificationService>();
                var trendService = scope.ServiceProvider.GetRequiredService<ITrendService>();

                _logger.LogInformation("Starting automatic vulnerability import from NVD");

                // Fetch bio-related vulnerabilities
                var imported = await nvdService.FetchAndImportVulnerabilitiesAsync(100, null);
                _logger.LogInformation($"Imported {imported} vulnerabilities");

                // Classify new vulnerabilities that don't have classification
                var unclassified = await scope.ServiceProvider
                    .GetRequiredService<BioShieldLens.Data.BioShieldDbContext>()
                    .Vulnerabilities
                    .Where(v => v.BioImpactScore == null || v.UrgencyLevel == null)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var vuln in unclassified)
                {
                    try
                    {
                        var classification = await aiService.ClassifyVulnerabilityAsync(vuln);
                        vuln.BioImpactScore = classification.BioImpactScore;
                        vuln.HumanImpactScore = classification.HumanImpactScore;
                        vuln.UrgencyLevel = classification.UrgencyLevel;
                        vuln.AffectedSector = classification.AffectedSector;
                        vuln.IntelNotes = classification.IntelNotes;
                        vuln.UpdatedAt = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error classifying vulnerability {vuln.CveId}");
                    }
                }

                await scope.ServiceProvider
                    .GetRequiredService<BioShieldLens.Data.BioShieldDbContext>()
                    .SaveChangesAsync(stoppingToken);

                // Calculate trends
                await trendService.CalculateTrendsAsync();

                _logger.LogInformation("Automatic import and classification completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background data service");
            }

            await Task.Delay(_period, stoppingToken);
        }
    }
}

