using BioShieldLens.Models;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace BioShieldLens.Services;

public class AiClassificationService : IAiClassificationService
{
    private readonly ILogger<AiClassificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly bool _useAi;

    public AiClassificationService(
        ILogger<AiClassificationService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _apiKey = _configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _useAi = true;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _logger.LogInformation("OpenAI API configured. Using AI-based classification.");
        }
        else
        {
            _useAi = false;
            _logger.LogInformation("No OpenAI API key found. Using keyword-based classification (no API key required).");
        }
    }

    public async Task<ClassificationResult> ClassifyVulnerabilityAsync(Vulnerability vulnerability)
    {
        if (_useAi)
        {
            try
            {
                return await ClassifyWithAIAsync(vulnerability);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI classification failed. Falling back to keyword-based classification.");
                return ClassifyWithKeywords(vulnerability);
            }
        }
        
        // Fallback to keyword-based classification
        return ClassifyWithKeywords(vulnerability);
    }

    private async Task<ClassificationResult> ClassifyWithAIAsync(Vulnerability vulnerability)
    {
        var prompt = $@"Analyze the following cybersecurity vulnerability and classify it for biological/biosecurity impact:

CVE ID: {vulnerability.CveId}
Severity Score: {vulnerability.SeverityScore?.ToString() ?? "Unknown"}
Description: {vulnerability.Description}

Please provide a JSON response with the following structure:
{{
  ""bioImpactScore"": <decimal 0-10, assessing impact on biological systems, healthcare, biotech, agriculture>,
  ""humanImpactScore"": <decimal 0-10, assessing direct human safety impact>,
  ""urgencyLevel"": <""Critical to Act Now"", ""Monitor"", or ""Low Priority"">,
  ""affectedSector"": <""Healthcare"", ""Biotech"", ""Agriculture"", ""General"", or other specific sector>,
  ""intelNotes"": <brief 1-2 sentence explanation of the biological/biosecurity implications>
}}

Consider:
- Healthcare/medical systems: High bio and human impact
- Biotechnology/laboratory systems: Moderate to high bio impact
- Agricultural/food systems: Moderate bio impact
- General IT systems: Lower bio impact unless they support critical biological infrastructure
- Severity score determines urgency level: 6.67-10 = Critical to Act Now, 3.34-6.66 = Monitor, 0-3.33 = Low Priority

Respond ONLY with valid JSON, no additional text.";

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 500
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("chat/completions", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        var choices = jsonDoc.RootElement.GetProperty("choices");
        var message = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        
        // Parse JSON response from AI
        try
        {
            var resultDoc = JsonDocument.Parse(message);
            var root = resultDoc.RootElement;
            
            // Helper to parse decimal from JSON (handles both string and number)
            decimal ParseDecimal(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    return decimal.TryParse(element.GetString(), out var result) ? result : 0m;
                }
                else if (element.ValueKind == JsonValueKind.Number)
                {
                    return element.GetDecimal();
                }
                return 0m;
            }
            
            var severityScore = vulnerability.SeverityScore ?? 0m;
            var aiUrgency = root.TryGetProperty("urgencyLevel", out var urgency) 
                ? urgency.GetString() ?? DetermineUrgencyLevel(severityScore)
                : DetermineUrgencyLevel(severityScore);
            
            return new ClassificationResult
            {
                BioImpactScore = root.TryGetProperty("bioImpactScore", out var bioScore) 
                    ? ParseDecimal(bioScore)
                    : severityScore,
                HumanImpactScore = root.TryGetProperty("humanImpactScore", out var humanScore) 
                    ? ParseDecimal(humanScore)
                    : severityScore,
                UrgencyLevel = DetermineUrgencyLevel(severityScore), // Always use severity-based classification
                AffectedSector = root.TryGetProperty("affectedSector", out var sector) 
                    ? sector.GetString() 
                    : "General",
                IntelNotes = root.TryGetProperty("intelNotes", out var notes) 
                    ? notes.GetString() 
                    : ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON. Content: {Content}", message);
            throw;
        }
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

        var severityScore = vulnerability.SeverityScore ?? 0m;
        
        if (medicalKeywords.Any(k => description.Contains(k)))
        {
            bioImpactScore = 8m;
            humanImpactScore = 9m;
            urgencyLevel = DetermineUrgencyLevel(severityScore);
            affectedSector = "Healthcare";
            intelNotes = "This vulnerability affects healthcare systems and could impact patient safety.";
        }
        else if (biotechKeywords.Any(k => description.Contains(k)))
        {
            bioImpactScore = 7m;
            humanImpactScore = 6m;
            urgencyLevel = DetermineUrgencyLevel(severityScore);
            affectedSector = "Biotech";
            intelNotes = "This vulnerability affects biotechnology or laboratory systems.";
        }
        else if (agricultureKeywords.Any(k => description.Contains(k)))
        {
            bioImpactScore = 6m;
            humanImpactScore = 5m;
            urgencyLevel = DetermineUrgencyLevel(severityScore);
            affectedSector = "Agriculture";
            intelNotes = "This vulnerability affects agricultural systems.";
        }
        else
        {
            // General classification based on severity
            bioImpactScore = severityScore;
            humanImpactScore = severityScore;
            urgencyLevel = DetermineUrgencyLevel(severityScore);
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

        if (_useAi)
        {
            try
            {
                return await GenerateIntelNotesWithAIAsync(vulnerabilities);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI intel notes generation failed. Falling back to keyword-based summary.");
            }
        }

        // Fallback to keyword-based summary
        return GenerateIntelNotesWithKeywords(vulnerabilities);
    }

    private async Task<string> GenerateIntelNotesWithAIAsync(List<Vulnerability> vulnerabilities)
    {
        var vulnerabilitySummary = string.Join("\n", vulnerabilities.Take(15).Select(v => 
            $"- {v.CveId} (Severity: {v.SeverityScore?.ToString("F1") ?? "N/A"}, Sector: {v.AffectedSector ?? "General"}, Urgency: {v.UrgencyLevel ?? "Monitor"}): {v.Description.Substring(0, Math.Min(250, v.Description.Length))}..."));
        
        var criticalCount = vulnerabilities.Count(v => v.UrgencyLevel == "Critical to Act Now");
        var monitorCount = vulnerabilities.Count(v => v.UrgencyLevel == "Monitor");
        var sectorGroups = vulnerabilities
            .Where(v => !string.IsNullOrEmpty(v.AffectedSector))
            .GroupBy(v => v.AffectedSector)
            .Select(g => new { Sector = g.Key, Count = g.Count(), Critical = g.Count(v => v.UrgencyLevel == "Critical to Act Now") })
            .OrderByDescending(x => x.Count)
            .Take(5);
        
        var systemMessage = @"You are a cybersecurity intelligence analyst specializing in biosecurity. Provide concise, actionable intelligence briefings. Keep responses brief and focused.";

        var prompt = $@"Analyze {vulnerabilities.Count} cybersecurity vulnerabilities from a biosecurity perspective.

Sample Vulnerabilities (showing top {Math.Min(10, vulnerabilities.Count)} of {vulnerabilities.Count}):
{vulnerabilitySummary}

Statistics:
- Total critical vulnerabilities: {criticalCount}
- Total requiring monitoring: {monitorCount}
- Top affected sectors: {string.Join(", ", sectorGroups.Select(s => $"{s.Sector} ({s.Count} total, {s.Critical} critical)"))}

Provide a CONCISE intelligence briefing in the following EXACT HTML format. Keep it brief - 1-2 sentences per bullet point:

<div class=""intel-summary"">
  <div class=""intel-section"">
    <h5 class=""intel-heading""><i class=""bi bi-exclamation-triangle-fill""></i> Executive Summary</h5>
    <p class=""intel-text"">[1-2 sentence overview highlighting {criticalCount} critical vulnerabilities and main risks to biological systems.]</p>
  </div>

  <div class=""intel-section"">
    <h5 class=""intel-heading""><i class=""bi bi-shield-exclamation""></i> Critical Threats</h5>
    <ul class=""intel-list"">
      <li><strong>[Top CVE ID]:</strong> [1 sentence: impact and why critical]</li>
      <li><strong>[Second CVE ID]:</strong> [1 sentence: impact and why critical]</li>
      <li><strong>[Third CVE ID]:</strong> [1 sentence: impact and why critical]</li>
    </ul>
  </div>

  <div class=""intel-section"">
    <h5 class=""intel-heading""><i class=""bi bi-building""></i> Sector Impact</h5>
    <ul class=""intel-list"">
      <li><strong>[Sector Name]:</strong> [1 sentence: number affected and key risk]</li>
      <li><strong>[Sector Name]:</strong> [1 sentence: number affected and key risk]</li>
    </ul>
  </div>

  <div class=""intel-section"">
    <h5 class=""intel-heading""><i class=""bi bi-check-circle-fill""></i> Recommended Actions</h5>
    <ol class=""intel-list"">
      <li><strong>Immediate:</strong> [1 sentence: priority action]</li>
      <li><strong>Short-term:</strong> [1 sentence: next steps]</li>
    </ol>
  </div>
</div>

IMPORTANT: 
- Keep ALL text brief (1-2 sentences max per item)
- Use actual CVE IDs from the sample
- Focus on biological/biosecurity impact
- Be concise and actionable";

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 800
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("chat/completions", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        var choices = jsonDoc.RootElement.GetProperty("choices");
        var message = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        // Clean up the response - remove any markdown code blocks if present
        message = message.Trim();
        if (message.StartsWith("```html"))
        {
            message = message.Substring(7);
        }
        if (message.StartsWith("```"))
        {
            message = message.Substring(3);
        }
        if (message.EndsWith("```"))
        {
            message = message.Substring(0, message.Length - 3);
        }
        message = message.Trim();

        // Ensure we have the basic structure - check for new format or old format
        if (!message.Contains("<div class=\"intel-summary\">") && 
            !message.Contains("intel-section") && 
            !message.Contains("<h5") && 
            !message.Contains("<h6>") && 
            !message.Contains("<ul>"))
        {
            // If AI didn't follow format, fall back to keyword-based
            _logger.LogWarning("AI response did not follow structured format. Falling back to keyword-based summary.");
            return GenerateIntelNotesWithKeywords(vulnerabilities);
        }

        return message;
    }

    private string GenerateIntelNotesWithKeywords(List<Vulnerability> vulnerabilities)
    {
        // Generate structured summary based on patterns
        var criticalCount = vulnerabilities.Count(v => v.UrgencyLevel == "Critical to Act Now");
        var monitorCount = vulnerabilities.Count(v => v.UrgencyLevel == "Monitor");
        var healthcareCount = vulnerabilities.Count(v => v.AffectedSector == "Healthcare");
        var biotechCount = vulnerabilities.Count(v => v.AffectedSector == "Biotech");
        var agricultureCount = vulnerabilities.Count(v => v.AffectedSector == "Agriculture");
        var totalCount = vulnerabilities.Count;

        var topThreats = vulnerabilities
            .OrderByDescending(v => v.SeverityScore ?? 0)
            .Take(3)
            .ToList();

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("<div class=\"intel-summary\">");
        
        // Executive Summary
        summary.AppendLine("  <div class=\"intel-section\">");
        summary.AppendLine("    <h5 class=\"intel-heading\"><i class=\"bi bi-exclamation-triangle-fill\"></i> Executive Summary</h5>");
        summary.AppendLine($"    <p class=\"intel-text\">{criticalCount} critical vulnerabilities identified requiring immediate action, posing significant risks to biological systems and healthcare infrastructure.</p>");
        summary.AppendLine("  </div>");

        // Critical Threats
        summary.AppendLine("  <div class=\"intel-section\">");
        summary.AppendLine("    <h5 class=\"intel-heading\"><i class=\"bi bi-shield-exclamation\"></i> Critical Threats</h5>");
        summary.AppendLine("    <ul class=\"intel-list\">");
        
        foreach (var threat in topThreats)
        {
            var severity = threat.SeverityScore?.ToString("F1") ?? "N/A";
            var sector = threat.AffectedSector ?? "General";
            summary.AppendLine($"      <li><strong>{threat.CveId}:</strong> Severity {severity} affecting {sector} systems - requires immediate patching.</li>");
        }
        
        summary.AppendLine("    </ul>");
        summary.AppendLine("  </div>");

        // Sector Impact
        summary.AppendLine("  <div class=\"intel-section\">");
        summary.AppendLine("    <h5 class=\"intel-heading\"><i class=\"bi bi-building\"></i> Sector Impact</h5>");
        summary.AppendLine("    <ul class=\"intel-list\">");
        
        if (healthcareCount > 0)
        {
            var criticalHealthcare = vulnerabilities.Count(v => v.AffectedSector == "Healthcare" && v.UrgencyLevel == "Critical to Act Now");
            summary.AppendLine($"      <li><strong>Healthcare:</strong> {healthcareCount} vulnerabilities ({criticalHealthcare} critical) affecting patient care systems and medical devices.</li>");
        }
        
        if (biotechCount > 0)
        {
            var criticalBiotech = vulnerabilities.Count(v => v.AffectedSector == "Biotech" && v.UrgencyLevel == "Critical to Act Now");
            summary.AppendLine($"      <li><strong>Biotechnology:</strong> {biotechCount} vulnerabilities ({criticalBiotech} critical) impacting laboratory systems and research data.</li>");
        }
        
        if (agricultureCount > 0)
        {
            var criticalAg = vulnerabilities.Count(v => v.AffectedSector == "Agriculture" && v.UrgencyLevel == "Critical to Act Now");
            summary.AppendLine($"      <li><strong>Agriculture:</strong> {agricultureCount} vulnerabilities ({criticalAg} critical) affecting food safety and crop management systems.</li>");
        }
        
        if (healthcareCount == 0 && biotechCount == 0 && agricultureCount == 0)
        {
            summary.AppendLine("      <li><strong>General Infrastructure:</strong> Vulnerabilities span multiple sectors affecting general IT infrastructure supporting biosecurity operations.</li>");
        }
        
        summary.AppendLine("    </ul>");
        summary.AppendLine("  </div>");

        // Recommended Actions
        summary.AppendLine("  <div class=\"intel-section\">");
        summary.AppendLine("    <h5 class=\"intel-heading\"><i class=\"bi bi-check-circle-fill\"></i> Recommended Actions</h5>");
        summary.AppendLine("    <ol class=\"intel-list\">");
        
        if (criticalCount > 0)
        {
            summary.AppendLine($"      <li><strong>Immediate:</strong> Patch all {criticalCount} critical vulnerabilities and isolate affected systems if patching cannot be completed immediately.</li>");
        }
        else
        {
            summary.AppendLine("      <li><strong>Immediate:</strong> Review vulnerabilities and prioritize based on infrastructure. Ensure monitoring systems are active.</li>");
        }
        
        summary.AppendLine("      <li><strong>Short-term:</strong> Complete remediation for high-severity items and conduct security assessments of affected systems.</li>");
        
        summary.AppendLine("    </ol>");
        summary.AppendLine("  </div>");

        summary.AppendLine("</div>");

        return summary.ToString();
    }

    /// <summary>
    /// Determines urgency level based on severity score using new criteria:
    /// Critical: 6.67-10
    /// Monitor: 3.34-6.66
    /// Low Priority: 0-3.33
    /// </summary>
    private string DetermineUrgencyLevel(decimal severityScore)
    {
        if (severityScore >= 6.67m)
        {
            return "Critical to Act Now";
        }
        else if (severityScore >= 3.34m)
        {
            return "Monitor";
        }
        else
        {
            return "Low Priority";
        }
    }
}

