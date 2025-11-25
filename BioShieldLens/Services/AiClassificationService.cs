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
- Severity score should influence urgency level (9+ = Critical, 7-8.9 = Monitor, <7 = Low Priority unless bio impact is high)

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
            
            return new ClassificationResult
            {
                BioImpactScore = root.TryGetProperty("bioImpactScore", out var bioScore) 
                    ? ParseDecimal(bioScore)
                    : vulnerability.SeverityScore ?? 0m,
                HumanImpactScore = root.TryGetProperty("humanImpactScore", out var humanScore) 
                    ? ParseDecimal(humanScore)
                    : vulnerability.SeverityScore ?? 0m,
                UrgencyLevel = root.TryGetProperty("urgencyLevel", out var urgency) 
                    ? urgency.GetString() ?? "Monitor" 
                    : "Monitor",
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
        var vulnerabilitySummary = string.Join("\n", vulnerabilities.Take(10).Select(v => 
            $"- {v.CveId} ({v.SeverityScore?.ToString("F1") ?? "N/A"}): {v.Description.Substring(0, Math.Min(200, v.Description.Length))}..."));
        
        var systemMessage = @"You are a cybersecurity analyst specializing in biosecurity. You MUST respond ONLY in the exact HTML format specified. Do NOT write paragraphs. Use ONLY bullet points in HTML lists.";

        var prompt = $@"Analyze these {vulnerabilities.Count} vulnerabilities from a biosecurity perspective.

Vulnerabilities:
{vulnerabilitySummary}

CRITICAL: Respond ONLY in this EXACT HTML format. NO paragraphs. NO explanations. Just the HTML structure below:

<h6>Top Threats:</h6>
<ul>
<li>[One sentence about most critical threat]</li>
<li>[One sentence about second critical threat]</li>
<li>[One sentence about third critical threat]</li>
</ul>

<h6>Affected Sectors:</h6>
<ul>
<li>[Sector name]: [brief impact description]</li>
<li>[Sector name]: [brief impact description]</li>
</ul>

<h6>Action Required:</h6>
<ul>
<li>[One actionable priority item]</li>
<li>[One actionable priority item]</li>
</ul>

Each bullet point must be ONE short sentence. Focus on biological/biosecurity implications.";

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 400
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

        // Ensure we have the basic structure
        if (!message.Contains("<h6>") && !message.Contains("<ul>"))
        {
            // If AI didn't follow format, fall back to keyword-based
            _logger.LogWarning("AI response did not follow structured format. Falling back to keyword-based summary.");
            return GenerateIntelNotesWithKeywords(vulnerabilities);
        }

        return message;
    }

    private string GenerateIntelNotesWithKeywords(List<Vulnerability> vulnerabilities)
    {
        // Generate simple structured summary based on patterns
        var criticalCount = vulnerabilities.Count(v => v.UrgencyLevel == "Critical to Act Now");
        var healthcareCount = vulnerabilities.Count(v => v.AffectedSector == "Healthcare");
        var biotechCount = vulnerabilities.Count(v => v.AffectedSector == "Biotech");
        var agricultureCount = vulnerabilities.Count(v => v.AffectedSector == "Agriculture");

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("<h6>Top Threats:</h6>");
        summary.AppendLine("<ul>");
        
        if (criticalCount > 0)
        {
            summary.AppendLine($"<li>{criticalCount} vulnerabilities require immediate action</li>");
        }
        
        var topThreats = vulnerabilities
            .OrderByDescending(v => v.SeverityScore ?? 0)
            .Take(3)
            .Select(v => $"{v.CveId} (Severity: {v.SeverityScore?.ToString("F1") ?? "N/A"})");
        
        foreach (var threat in topThreats)
        {
            summary.AppendLine($"<li>{threat}</li>");
        }
        
        summary.AppendLine("</ul>");
        summary.AppendLine("<h6>Affected Sectors:</h6>");
        summary.AppendLine("<ul>");
        
        if (healthcareCount > 0)
        {
            summary.AppendLine($"<li>Healthcare: {healthcareCount} vulnerabilities</li>");
        }
        
        if (biotechCount > 0)
        {
            summary.AppendLine($"<li>Biotech: {biotechCount} vulnerabilities</li>");
        }
        
        if (agricultureCount > 0)
        {
            summary.AppendLine($"<li>Agriculture: {agricultureCount} vulnerabilities</li>");
        }
        
        if (healthcareCount == 0 && biotechCount == 0 && agricultureCount == 0)
        {
            summary.AppendLine("<li>General: Multiple sectors affected</li>");
        }
        
        summary.AppendLine("</ul>");
        summary.AppendLine("<h6>Action Required:</h6>");
        summary.AppendLine("<ul>");
        
        if (criticalCount > 0)
        {
            summary.AppendLine("<li>Prioritize patching for critical vulnerabilities immediately</li>");
        }
        
        summary.AppendLine("<li>Review individual vulnerabilities for detailed impact assessment</li>");
        summary.AppendLine("</ul>");

        return summary.ToString();
    }
}

