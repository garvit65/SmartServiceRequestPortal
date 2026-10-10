using System.ComponentModel.DataAnnotations;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Domain.Entities;

public class ServiceRequest
{
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public RequestPriority Priority { get; set; } = RequestPriority.Medium;
    public RequestStatus Status { get; set; } = RequestStatus.New;
    public ContactMethod PreferredContactMethod { get; set; } = ContactMethod.Email;

    [Required]
    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser? Requester { get; set; }

    public int? AssignedTeamId { get; set; }
    public SupportTeam? AssignedTeam { get; set; }

    public string? AssigneeId { get; set; }
    public ApplicationUser? Assignee { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime ResponseDueAtUtc { get; set; }
    public DateTime ResolutionDueAtUtc { get; set; }
    public DateTime? FirstRespondedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    [MaxLength(1000)]
    public string? ResolutionSummary { get; set; }

    [MaxLength(100)]
    public string? ResolutionCategory { get; set; }

    public bool IsSlaBreached { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<RequestComment> Comments { get; set; } = new List<RequestComment>();
    public ICollection<RequestAttachment> Attachments { get; set; } = new List<RequestAttachment>();
    public ICollection<RequestStatusHistory> StatusHistory { get; set; } = new List<RequestStatusHistory>();
    public ICollection<AssignmentHistory> AssignmentHistory { get; set; } = new List<AssignmentHistory>();
}
