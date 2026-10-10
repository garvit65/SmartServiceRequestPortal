using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;

namespace ServicePortal.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IApplicationDbContext context, ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(
        string? actorId,
        string actorName,
        string action,
        string entityName,
        string entityId,
        string changeSummary,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = new AuditLog
            {
                ActorId = actorId,
                ActorName = actorName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                ChangeSummary = changeSummary,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log for Action: {Action}, Entity: {EntityName}", action, entityName);
        }
    }
}
