using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class RequestAttachment
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public int? RequestCommentId { get; set; }
    public RequestComment? RequestComment { get; set; }

    [Required]
    [MaxLength(260)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [Required]
    public string UploadedById { get; set; } = string.Empty;
    public ApplicationUser? UploadedBy { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
