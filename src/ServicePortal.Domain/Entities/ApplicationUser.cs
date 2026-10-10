using Microsoft.AspNetCore.Identity;

namespace ServicePortal.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int? SupportTeamId { get; set; }
    public SupportTeam? SupportTeam { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceRequest> CreatedRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<ServiceRequest> AssignedRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<RequestComment> Comments { get; set; } = new List<RequestComment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
