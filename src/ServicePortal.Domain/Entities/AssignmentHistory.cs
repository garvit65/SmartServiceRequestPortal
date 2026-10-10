using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class AssignmentHistory
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public string? PreviousAssigneeId { get; set; }
    public ApplicationUser? PreviousAssignee { get; set; }

    [Required]
    public string NewAssigneeId { get; set; } = string.Empty;
    public ApplicationUser? NewAssignee { get; set; }

    [Required]
    public string AssignedById { get; set; } = string.Empty;
    public ApplicationUser? AssignedBy { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}
