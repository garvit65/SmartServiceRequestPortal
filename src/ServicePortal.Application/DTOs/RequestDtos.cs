using System.ComponentModel.DataAnnotations;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.DTOs;

public class CreateRequestDto
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(4000, MinimumLength = 5, ErrorMessage = "Description must be between 5 and 4000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a category.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid category.")]
    public int CategoryId { get; set; }

    public RequestPriority Priority { get; set; } = RequestPriority.Medium;

    public ContactMethod PreferredContactMethod { get; set; } = ContactMethod.Email;

    public bool WarningAcknowledged { get; set; }
}

public class AssignRequestDto
{
    public int RequestId { get; set; }

    [Required(ErrorMessage = "Assignee is required.")]
    public string AssigneeId { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Reason { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public class ResolveRequestDto
{
    public int RequestId { get; set; }

    [Required(ErrorMessage = "Resolution summary is required.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Resolution summary must be between 5 and 1000 characters.")]
    public string ResolutionSummary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Resolution category is required.")]
    [StringLength(100)]
    public string ResolutionCategory { get; set; } = string.Empty;

    public string RowVersion { get; set; } = string.Empty;
}

public class ReopenRequestDto
{
    public int RequestId { get; set; }

    [Required(ErrorMessage = "Reason for reopening is required.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    public string RowVersion { get; set; } = string.Empty;
}

public class AddCommentDto
{
    public int RequestId { get; set; }

    [Required(ErrorMessage = "Comment body is required.")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "Comment cannot be empty.")]
    public string Body { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}

public class RequestFilterDto
{
    public string? SearchTerm { get; set; }
    public RequestStatus? Status { get; set; }
    public int? CategoryId { get; set; }
    public RequestPriority? Priority { get; set; }
    public int? TeamId { get; set; }
    public string? AssigneeId { get; set; }
    public string? RequesterId { get; set; }
    public SlaStatus? SlaState { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class RequestListItemDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; }
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ResponseDueAtUtc { get; set; }
    public DateTime ResolutionDueAtUtc { get; set; }
    public SlaStatus SlaStatus { get; set; }
    public bool IsSlaBreached { get; set; }
}

public class RequestDetailsDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; }
    public ContactMethod PreferredContactMethod { get; set; }
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime ResponseDueAtUtc { get; set; }
    public DateTime ResolutionDueAtUtc { get; set; }
    public DateTime? FirstRespondedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ResolutionSummary { get; set; }
    public string? ResolutionCategory { get; set; }
    public bool IsSlaBreached { get; set; }
    public SlaStatus SlaStatus { get; set; }
    public string RowVersion { get; set; } = string.Empty;

    public bool CanReopen { get; set; }
    public bool CanClose { get; set; }

    public List<CommentItemDto> Comments { get; set; } = new();
    public List<AttachmentItemDto> Attachments { get; set; } = new();
    public List<ActivityTimelineItemDto> Timeline { get; set; } = new();
}

public class CommentItemDto
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class AttachmentItemDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
}

public class ActivityTimelineItemDto
{
    public DateTime TimestampUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class DashboardMetricsDto
{
    public string UserRole { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int WaitingForRequesterCount { get; set; }
    public int ResolvedCount { get; set; }
    public int ClosedCount { get; set; }
    public int DueSoonCount { get; set; }
    public int BreachedCount { get; set; }
    public double SlaCompliancePercentage { get; set; }
    public double AverageResolutionHours { get; set; }
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
    public Dictionary<string, int> PriorityDistribution { get; set; } = new();
    public Dictionary<string, int> CategoryVolume { get; set; } = new();
    public Dictionary<string, int> AgentWorkload { get; set; } = new();
    public List<RequestListItemDto> RecentRequests { get; set; } = new();
}
