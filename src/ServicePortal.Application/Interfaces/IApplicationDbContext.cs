using Microsoft.EntityFrameworkCore;
using ServicePortal.Domain.Entities;

namespace ServicePortal.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<SupportTeam> SupportTeams { get; }
    DbSet<Category> Categories { get; }
    DbSet<SlaPolicy> SlaPolicies { get; }
    DbSet<ServiceRequest> ServiceRequests { get; }
    DbSet<RequestComment> RequestComments { get; }
    DbSet<RequestAttachment> RequestAttachments { get; }
    DbSet<RequestStatusHistory> RequestStatusHistories { get; }
    DbSet<AssignmentHistory> AssignmentHistories { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
