using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Services;
using ServicePortal.Domain.Entities;
using ServicePortal.Infrastructure.Data;
using Xunit;

namespace ServicePortal.UnitTests;

public class ReferenceNumberGeneratorTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GenerateNextReferenceAsync_GeneratesCorrectPrefixAndFormatting()
    {
        using var context = CreateInMemoryContext();
        var sut = new ReferenceNumberGenerator(context);

        var refNum = await sut.GenerateNextReferenceAsync();

        var year = DateTime.UtcNow.Year;
        Assert.StartsWith($"SR-{year}-", refNum);
        Assert.Equal($"SR-{year}-000001", refNum);
    }

    [Fact]
    public async Task GenerateNextReferenceAsync_ConcurrentCalls_GeneratesUniqueSequences()
    {
        using var context = CreateInMemoryContext();
        var sut = new ReferenceNumberGenerator(context);

        var tasks = Enumerable.Range(1, 10).Select(async _ =>
        {
            var reference = await sut.GenerateNextReferenceAsync();
            context.ServiceRequests.Add(new ServiceRequest
            {
                ReferenceNumber = reference,
                Title = "Test",
                Description = "Desc",
                RequesterId = "user1"
            });
            await context.SaveChangesAsync();
            return reference;
        }).ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Distinct().Count());
    }
}
