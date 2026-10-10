using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Services;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;
using Xunit;

namespace ServicePortal.UnitTests;

public class DuplicateRequestDetectorTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_WhenSimilarActiveRequestExistsWithin24Hours_DetectsDuplicate()
    {
        using var context = CreateInMemoryContext();
        var sut = new DuplicateRequestDetector(context);

        context.ServiceRequests.Add(new ServiceRequest
        {
            ReferenceNumber = "SR-2026-000100",
            RequesterId = "user1",
            CategoryId = 1,
            Title = "Laptop screen flickering intermittently",
            Description = "Details",
            Status = RequestStatus.InProgress,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        });
        await context.SaveChangesAsync();

        var (isDup, existing) = await sut.CheckForDuplicateAsync("user1", 1, "laptop screen flickering intermittently");

        Assert.True(isDup);
        Assert.NotNull(existing);
        Assert.Equal("SR-2026-000100", existing.ReferenceNumber);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_WhenNoRecentRequest_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var sut = new DuplicateRequestDetector(context);

        var (isDup, existing) = await sut.CheckForDuplicateAsync("user1", 1, "Brand new unique request");

        Assert.False(isDup);
        Assert.Null(existing);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_WhenPreviousRequestIsClosed_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var sut = new DuplicateRequestDetector(context);

        context.ServiceRequests.Add(new ServiceRequest
        {
            ReferenceNumber = "SR-2026-000101",
            RequesterId = "user1",
            CategoryId = 1,
            Title = "Network connection issue",
            Description = "Details",
            Status = RequestStatus.Closed, // Closed tickets do not block new submissions!
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var (isDup, existing) = await sut.CheckForDuplicateAsync("user1", 1, "Network connection issue");

        Assert.False(isDup);
        Assert.Null(existing);
    }
}
