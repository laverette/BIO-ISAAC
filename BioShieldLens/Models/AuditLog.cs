using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BioShieldLens.Models;

[Table("AuditLogs")]
public class AuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty; // Import, Export, Classify, StatusChange, etc.

    [MaxLength(100)]
    public string? EntityType { get; set; } // Vulnerability, Trend, etc.

    public int? EntityId { get; set; }

    [Column(TypeName = "TEXT")]
    public string? Details { get; set; }

    [MaxLength(100)]
    public string? PerformedBy { get; set; } = "System";

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

