namespace ServicePortal.Domain.Entities;

public class SupportTeam
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<ServiceRequest> AssignedRequests { get; set; } = new List<ServiceRequest>();
}
