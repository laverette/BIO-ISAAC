using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BioShieldLens.Models;

[Table("Trends")]
public class Trend
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Month { get; set; } = string.Empty;

    [Column(TypeName = "DECIMAL(10,2)")]
    public decimal? ChangePercentage { get; set; }

    [Column(TypeName = "TEXT")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

