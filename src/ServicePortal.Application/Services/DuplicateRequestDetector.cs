using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Services;

public class DuplicateRequestDetector
{
    private readonly IApplicationDbContext _context;

    public DuplicateRequestDetector(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(bool IsDuplicateDetected, ServiceRequest? ExistingRequest)> CheckForDuplicateAsync(
        string requesterId,
        int categoryId,
        string title,
        int windowHours = 24,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-windowHours);
        var normalizedTitle = title.Trim().ToLower();

        var existing = await _context.ServiceRequests
            .Where(r => r.RequesterId == requesterId &&
                        r.CategoryId == categoryId &&
                        r.CreatedAtUtc >= cutoff &&
                        r.Status != RequestStatus.Closed)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null && (existing.Title.Trim().ToLower() == normalizedTitle ||
            existing.Title.ToLower().Contains(normalizedTitle) ||
            normalizedTitle.Contains(existing.Title.ToLower())))
        {
            return (true, existing);
        }

        return (false, null);
    }
}
