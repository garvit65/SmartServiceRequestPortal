using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;

namespace ServicePortal.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SupportTeam> SupportTeams => Set<SupportTeam>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<RequestComment> RequestComments => Set<RequestComment>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();
    public DbSet<RequestStatusHistory> RequestStatusHistories => Set<RequestStatusHistory>();
    public DbSet<AssignmentHistory> AssignmentHistories => Set<AssignmentHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ServiceRequest Indexes and Relationships
        builder.Entity<ServiceRequest>(entity =>
        {
            entity.HasIndex(r => r.ReferenceNumber).IsUnique();
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.CreatedAtUtc);
            entity.HasIndex(r => r.ResolutionDueAtUtc);

            entity.HasOne(r => r.Requester)
                .WithMany(u => u.CreatedRequests)
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Assignee)
                .WithMany(u => u.AssignedRequests)
                .HasForeignKey(r => r.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.AssignedTeam)
                .WithMany(t => t.AssignedRequests)
                .HasForeignKey(r => r.AssignedTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Category)
                .WithMany(c => c.Requests)
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Category & SupportTeam
        builder.Entity<Category>(entity =>
        {
            entity.HasOne(c => c.SupportTeam)
                .WithMany(t => t.Categories)
                .HasForeignKey(c => c.SupportTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RequestComment
        builder.Entity<RequestComment>(entity =>
        {
            entity.HasOne(c => c.ServiceRequest)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Author)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RequestAttachment
        builder.Entity<RequestAttachment>(entity =>
        {
            entity.HasOne(a => a.ServiceRequest)
                .WithMany(r => r.Attachments)
                .HasForeignKey(a => a.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RequestStatusHistory
        builder.Entity<RequestStatusHistory>(entity =>
        {
            entity.HasOne(h => h.ServiceRequest)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(h => h.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.ChangedBy)
                .WithMany()
                .HasForeignKey(h => h.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AssignmentHistory
        builder.Entity<AssignmentHistory>(entity =>
        {
            entity.HasOne(h => h.ServiceRequest)
                .WithMany(r => r.AssignmentHistory)
                .HasForeignKey(h => h.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.NewAssignee)
                .WithMany()
                .HasForeignKey(h => h.NewAssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.PreviousAssignee)
                .WithMany()
                .HasForeignKey(h => h.PreviousAssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.AssignedBy)
                .WithMany()
                .HasForeignKey(h => h.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Notification
        builder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.Recipient)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.RelatedRequest)
                .WithMany()
                .HasForeignKey(n => n.RelatedRequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditLog
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.TimestampUtc);
            entity.HasIndex(a => a.CorrelationId);
        });
    }
}
