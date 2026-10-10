using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string RecipientId { get; set; } = string.Empty;
    public ApplicationUser? Recipient { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public int? RelatedRequestId { get; set; }
    public ServiceRequest? RelatedRequest { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
