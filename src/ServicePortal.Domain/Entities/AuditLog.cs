using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public string? ActorId { get; set; }

    [MaxLength(100)]
    public string ActorName { get; set; } = "System";

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string ChangeSummary { get; set; } = string.Empty;

    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
