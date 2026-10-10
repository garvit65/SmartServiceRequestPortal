using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Infrastructure.BackgroundJobs;

public class RequestAutoClosureBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RequestAutoClosureBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public RequestAutoClosureBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RequestAutoClosureBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Request Auto-Closure Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAutoClosuresAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during automatic request closure execution.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Request Auto-Closure Background Service stopping.");
    }

    public async Task<int> ProcessAutoClosuresAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var cutoffUtc = DateTime.UtcNow.AddDays(-7);

        var eligibleRequests = await context.ServiceRequests
            .Where(r => r.Status == RequestStatus.Resolved &&
                        r.ResolvedAtUtc.HasValue &&
                        r.ResolvedAtUtc.Value <= cutoffUtc)
            .ToListAsync(cancellationToken);

        int closedCount = 0;

        foreach (var req in eligibleRequests)
        {
            req.Status = RequestStatus.Closed;
            req.ClosedAtUtc = DateTime.UtcNow;
            req.UpdatedAtUtc = DateTime.UtcNow;

            var history = new RequestStatusHistory
            {
                ServiceRequestId = req.Id,
                FromStatus = RequestStatus.Resolved,
                ToStatus = RequestStatus.Closed,
                ChangedById = req.RequesterId, // System automation on behalf of ticket lifecycle
                Reason = "Automatically closed by system 7 days after resolution without customer dispute.",
                ChangedAtUtc = DateTime.UtcNow
            };
            context.RequestStatusHistories.Add(history);

            await auditService.LogAsync(
                null,
                "System Background Job",
                "AutoCloseRequest",
                nameof(ServiceRequest),
                req.Id.ToString(),
                $"Request {req.ReferenceNumber} was automatically closed after 7 calendar days in Resolved status.",
                null,
                cancellationToken);

            closedCount++;
            _logger.LogInformation("Auto-closed resolved request {Reference}", req.ReferenceNumber);
        }

        if (closedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return closedCount;
    }
}
