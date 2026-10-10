using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServicePortal.Application.DTOs;
using ServicePortal.Application.Exceptions;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.BackgroundJobs;
using ServicePortal.Infrastructure.Data;
using Xunit;

namespace ServicePortal.IntegrationTests;

public class PortalIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PortalIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC02_AuthorizationTest_RequesterCannotViewAnotherRequestersTicket()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();

        var cat = await context.Categories.FirstAsync();
        var requester1 = await context.Users.FirstAsync(u => u.Email == "john.requester@contoso.com");
        var requester2 = await context.Users.FirstAsync(u => u.Email == "sara.requester@contoso.com");

        // Create a ticket belonging to Requester1 (John)
        var ticket = new ServiceRequest
        {
            ReferenceNumber = "SR-TEST-000001",
            Title = "Confidential User A Ticket",
            Description = "Private details",
            CategoryId = cat.Id,
            RequesterId = requester1.Id,
            Status = RequestStatus.New,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        context.ServiceRequests.Add(ticket);
        await context.SaveChangesAsync();

        // Attempt to fetch as Requester2 (Sara) -> Must throw UnauthorizedAccessException (AC-02)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await requestService.GetRequestDetailsAsync(ticket.Id, requester2.Id, AppRoles.Requester);
        });
    }

    [Fact]
    public async Task AC03_RedactionTest_RequesterNeverReceivesInternalComments()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();

        var cat = await context.Categories.FirstAsync();
        var requester1 = await context.Users.FirstAsync(u => u.Email == "john.requester@contoso.com");
        var agent = await context.Users.FirstAsync(u => u.Email == "agent.it1@contoso.com");

        var ticket = new ServiceRequest
        {
            ReferenceNumber = "SR-TEST-000002",
            Title = "Network ticket with internal notes",
            Description = "Details",
            CategoryId = cat.Id,
            RequesterId = requester1.Id,
            Status = RequestStatus.InProgress,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        context.ServiceRequests.Add(ticket);
        await context.SaveChangesAsync();

        context.RequestComments.AddRange(
            new RequestComment { ServiceRequestId = ticket.Id, AuthorId = agent.Id, Visibility = CommentVisibility.Public, Body = "Public customer update." },
            new RequestComment { ServiceRequestId = ticket.Id, AuthorId = agent.Id, Visibility = CommentVisibility.Internal, Body = "SECRET internal diagnostic note." }
        );
        await context.SaveChangesAsync();

        // Fetch as Requester1
        var requesterView = await requestService.GetRequestDetailsAsync(ticket.Id, requester1.Id, AppRoles.Requester);

        Assert.NotNull(requesterView);
        Assert.Single(requesterView.Comments);
        Assert.DoesNotContain(requesterView.Comments, c => c.Body.Contains("SECRET"));
        Assert.DoesNotContain(requesterView.Timeline, t => t.Details.Contains("SECRET"));
    }

    [Fact]
    public async Task AC04_StateMachineTest_InvalidTransitionDirectlyToClosed_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var workflow = scope.ServiceProvider.GetRequiredService<IWorkflowService>();

        var cat = await context.Categories.FirstAsync();
        var requester = await context.Users.FirstAsync(u => u.Email == "john.requester@contoso.com");
        var agent = await context.Users.FirstAsync(u => u.Email == "agent.it1@contoso.com");

        var ticket = new ServiceRequest
        {
            ReferenceNumber = "SR-TEST-000003",
            Title = "New ticket attempting bypass",
            Description = "Details",
            CategoryId = cat.Id,
            RequesterId = requester.Id,
            Status = RequestStatus.New,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        context.ServiceRequests.Add(ticket);
        await context.SaveChangesAsync();

        // Attempting direct jump from New to Closed -> Must be rejected!
        await Assert.ThrowsAsync<WorkflowTransitionException>(async () =>
        {
            await workflow.ValidateAndTransitionAsync(ticket, RequestStatus.Closed, agent, AppRoles.SupportAgent);
        });
    }

    [Fact]
    public async Task AC10_ConcurrencyTest_MismatchedRowVersion_ThrowsConcurrencyConflictException()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();

        var cat = await context.Categories.FirstAsync();
        var requester = await context.Users.FirstAsync(u => u.Email == "john.requester@contoso.com");
        var agent = await context.Users.FirstAsync(u => u.Email == "agent.it1@contoso.com");
        var lead = await context.Users.FirstAsync(u => u.Email == "lead.it@contoso.com");

        var ticket = new ServiceRequest
        {
            ReferenceNumber = "SR-TEST-000004",
            Title = "Concurrency test ticket",
            Description = "Details",
            CategoryId = cat.Id,
            RequesterId = requester.Id,
            Status = RequestStatus.InProgress,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        context.ServiceRequests.Add(ticket);
        await context.SaveChangesAsync();

        // Stale client row version
        var staleRowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var assignDto = new AssignRequestDto
        {
            RequestId = ticket.Id,
            AssigneeId = agent.Id,
            RowVersion = staleRowVersion
        };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
        {
            await requestService.AssignRequestAsync(assignDto, lead.Id, AppRoles.TeamLead);
        });
    }

    [Fact]
    public async Task AC07_BackgroundJobTest_AutoClosureJob_ClosesEligibleResolvedRequests()
    {
        using var scope = _factory.Services.CreateScope();
        var context = (ApplicationDbContext)scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var autoCloseJob = scope.ServiceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<RequestAutoClosureBackgroundService>()
            .FirstOrDefault();

        Assert.NotNull(autoCloseJob);

        var cat = await context.Categories.FirstAsync();
        var requester = await context.Users.FirstAsync(u => u.Email == "john.requester@contoso.com");

        // Add request resolved 8 days ago
        var ticket = new ServiceRequest
        {
            ReferenceNumber = "SR-TEST-000005",
            Title = "Old resolved ticket",
            Description = "Details",
            CategoryId = cat.Id,
            RequesterId = requester.Id,
            Status = RequestStatus.Resolved,
            ResolvedAtUtc = DateTime.UtcNow.AddDays(-8),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        context.ServiceRequests.Add(ticket);
        await context.SaveChangesAsync();

        // Trigger auto-closure process directly
        var closedCount = await autoCloseJob.ProcessAutoClosuresAsync();

        Assert.True(closedCount >= 1);

        // Reload entity to reflect background save
        await context.Entry(ticket).ReloadAsync();
        Assert.Equal(RequestStatus.Closed, ticket.Status);
        Assert.NotNull(ticket.ClosedAtUtc);
    }
}
