using System.ComponentModel.DataAnnotations;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Domain.Entities;

public class RequestComment
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }

    public CommentVisibility Visibility { get; set; } = CommentVisibility.Public;

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
