using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Infrastructure.BackgroundJobs;

public class SlaMonitoringBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaMonitoringBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public SlaMonitoringBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SlaMonitoringBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SLA Monitoring Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSlaChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during SLA monitoring evaluation.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("SLA Monitoring Background Service stopping.");
    }

    public async Task ProcessSlaChecksAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var slaService = scope.ServiceProvider.GetRequiredService<ISlaCalculationService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var nowUtc = DateTime.UtcNow;

        var openRequests = await context.ServiceRequests
            .Where(r => r.Status != RequestStatus.Resolved && r.Status != RequestStatus.Closed)
            .ToListAsync(cancellationToken);

        foreach (var req in openRequests)
        {
            if (!req.IsSlaBreached && slaService.IsBreached(req, nowUtc))
            {
                req.IsSlaBreached = true;
                req.UpdatedAtUtc = nowUtc;

                await notificationService.NotifySlaBreachedAsync(req, cancellationToken);
                await auditService.LogAsync(
                    null,
                    "System Background Job",
                    "SlaBreached",
                    nameof(ServiceRequest),
                    req.Id.ToString(),
                    $"SLA breach detected for request {req.ReferenceNumber}.",
                    null,
                    cancellationToken);

                _logger.LogWarning("Request {Reference} breached SLA at {Time}", req.ReferenceNumber, nowUtc);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
