namespace ServicePortal.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SupportTeamId { get; set; }
    public SupportTeam? SupportTeam { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceRequest> Requests { get; set; } = new List<ServiceRequest>();
}
