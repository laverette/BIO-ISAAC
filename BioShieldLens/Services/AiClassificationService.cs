using BioShieldLens.Models;

namespace BioShieldLens.Services;

public class AiClassificationService : IAiClassificationService
{
    private readonly ILogger<AiClassificationService> _logger;

    public AiClassificationService(ILogger<AiClassificationService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Using keyword-based classification (no API key required).");
    }

    public async Task<ClassificationResult> ClassifyVulnerabilityAsync(Vulnerability vulnerability)
    {
        // Use keyword-based classification (works without any API key)
        return await Task.FromResult(ClassifyWithKeywords(vulnerability));
    }

    private ClassificationResult ClassifyWithKeywords(Vulnerability vulnerability)
    {
        var description = vulnerability.Description.ToLower();
        var bioImpactScore = 0m;
        var humanImpactScore = 0m;
        var urgencyLevel = "Monitor";
        var affectedSector = "General";
        var intelNotes = "";

        // Simple keyword-based classification
        var medicalKeywords = new[] { "medical", "hospital", "healthcare", "patient", "clinical" };
        var biotechKeywords = new[] { "biotech", "laboratory", "lab", "pharmaceutical", "genetic" };
        var agricultureKeywords = new[] { "agriculture", "farming", "crop", "livestock", "food" };

        if (medicalKeywords.Any(k => description.Contains(k)))
        {
            bioImpactScore = 8m;
            humanImpactScore = 9m;
            urgencyLevel = vulnerability.SeverityScore >= 7 ? "Critical to Act Now" : "Monitor";
            affectedSector = "Healthcare";
            intelNotes = "This vulnerability affects healthcare systems and could impact patient safety.";
        }
        else if (biotechKeywords.Any(k => description.Contains(k)))
        {
            bioImpactScore = 7m;
            humanImpactScore = 6m;
            urgencyLevel = vulnerability.SeverityScore >= 7 ? "Critical to Act Now" : "Monitor";
            affectedSector = "Biotech";
            intelNotes = "This vulnerability affects biotechnology or laboratory systems.";
        }
        else if (agricultureKeywords.Any(k => description.Contains(k)))
        {
            bioImpactScore = 6m;
            humanImpactScore = 5m;
            urgencyLevel = vulnerability.SeverityScore >= 7 ? "Monitor" : "Low Priority";
            affectedSector = "Agriculture";
            intelNotes = "This vulnerability affects agricultural systems.";
        }
        else
        {
            // General classification based on severity
            bioImpactScore = vulnerability.SeverityScore ?? 0m;
            humanImpactScore = vulnerability.SeverityScore ?? 0m;
            urgencyLevel = vulnerability.SeverityScore >= 9 ? "Critical to Act Now" :
                          vulnerability.SeverityScore >= 7 ? "Monitor" : "Low Priority";
        }

        return new ClassificationResult
        {
            BioImpactScore = bioImpactScore,
            HumanImpactScore = humanImpactScore,
            UrgencyLevel = urgencyLevel,
            AffectedSector = affectedSector,
            IntelNotes = intelNotes
        };
    }

    public async Task<string> GenerateIntelNotesAsync(List<Vulnerability> vulnerabilities)
    {
        if (!vulnerabilities.Any())
        {
            return "No vulnerabilities to analyze.";
        }

        // Generate simple summary based on patterns
        var criticalCount = vulnerabilities.Count(v => v.UrgencyLevel == "Critical to Act Now");
        var healthcareCount = vulnerabilities.Count(v => v.AffectedSector == "Healthcare");
        var biotechCount = vulnerabilities.Count(v => v.AffectedSector == "Biotech");
        var agricultureCount = vulnerabilities.Count(v => v.AffectedSector == "Agriculture");

        var summary = new System.Text.StringBuilder();
        summary.Append($"Analysis of {vulnerabilities.Count} vulnerabilities: ");
        
        if (criticalCount > 0)
        {
            summary.Append($"{criticalCount} require immediate attention. ");
        }
        
        if (healthcareCount > 0)
        {
            summary.Append($"Healthcare sector affected by {healthcareCount} vulnerabilities. ");
        }
        
        if (biotechCount > 0)
        {
            summary.Append($"Biotech sector shows {biotechCount} vulnerabilities. ");
        }
        
        if (agricultureCount > 0)
        {
            summary.Append($"Agriculture sector has {agricultureCount} vulnerabilities. ");
        }

        summary.Append("Review individual vulnerabilities for detailed impact assessment.");

        return await Task.FromResult(summary.ToString());
    }
}

