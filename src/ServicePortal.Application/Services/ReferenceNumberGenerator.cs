using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Interfaces;

namespace ServicePortal.Application.Services;

public class ReferenceNumberGenerator : IReferenceNumberGenerator
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IApplicationDbContext _context;

    public ReferenceNumberGenerator(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextReferenceAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var currentYear = DateTime.UtcNow.Year;
            var prefix = $"SR-{currentYear}-";

            // Count existing requests for current year
            var countForYear = await _context.ServiceRequests
                .Where(r => r.ReferenceNumber.StartsWith(prefix))
                .CountAsync(cancellationToken);

            var nextSequence = countForYear + 1;
            var reference = $"{prefix}{nextSequence:D6}";

            // Guarantee uniqueness in rare race scenarios
            while (await _context.ServiceRequests.AnyAsync(r => r.ReferenceNumber == reference, cancellationToken))
            {
                nextSequence++;
                reference = $"{prefix}{nextSequence:D6}";
            }

            return reference;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
