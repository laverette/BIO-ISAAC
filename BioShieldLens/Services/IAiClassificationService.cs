using BioShieldLens.Models;

namespace BioShieldLens.Services;

public interface IAiClassificationService
{
    Task<ClassificationResult> ClassifyVulnerabilityAsync(Vulnerability vulnerability);
    Task<string> GenerateIntelNotesAsync(List<Vulnerability> vulnerabilities);
}

public class ClassificationResult
{
    public decimal BioImpactScore { get; set; }
    public decimal HumanImpactScore { get; set; }
    public string UrgencyLevel { get; set; } = "Monitor";
    public string? AffectedSector { get; set; }
    public string? IntelNotes { get; set; }
}

