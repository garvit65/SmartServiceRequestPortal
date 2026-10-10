using System.ComponentModel.DataAnnotations;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Domain.Entities;

public class RequestStatusHistory
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public RequestStatus FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }

    [Required]
    public string ChangedById { get; set; } = string.Empty;
    public ApplicationUser? ChangedBy { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
