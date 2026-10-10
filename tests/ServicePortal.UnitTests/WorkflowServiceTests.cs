using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Exceptions;
using ServicePortal.Application.Services;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;
using Xunit;

namespace ServicePortal.UnitTests;

public class WorkflowServiceTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void CanTransition_NewToAssigned_ByTeamLead_ReturnsTrue()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var can = sut.CanTransition(RequestStatus.New, RequestStatus.Assigned, AppRoles.TeamLead, out var error);

        Assert.True(can);
        Assert.Empty(error);
    }

    [Fact]
    public void CanTransition_NewToAssigned_ByRequester_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var can = sut.CanTransition(RequestStatus.New, RequestStatus.Assigned, AppRoles.Requester, out var error);

        Assert.False(can);
        Assert.Contains("Only Team Leads or Administrators", error);
    }

    [Fact]
    public void CanTransition_AssignedToInProgress_ByAgent_ReturnsTrue()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var can = sut.CanTransition(RequestStatus.Assigned, RequestStatus.InProgress, AppRoles.SupportAgent, out var error);

        Assert.True(can);
    }

    [Fact]
    public void CanTransition_ClosedToAnyStatus_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var can = sut.CanTransition(RequestStatus.Closed, RequestStatus.InProgress, AppRoles.Administrator, out var error);

        Assert.False(can);
        Assert.Contains("Closed requests", error);
    }

    [Fact]
    public async Task ValidateAndTransition_InProgressToWaitingForRequester_WithoutReason_ThrowsValidationException()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var req = new ServiceRequest { Id = 1, Status = RequestStatus.InProgress, RowVersion = Guid.NewGuid().ToByteArray() };
        var actor = new ApplicationUser { Id = "agent1", FullName = "Support Agent" };

        await Assert.ThrowsAsync<DomainValidationException>(async () =>
        {
            await sut.ValidateAndTransitionAsync(req, RequestStatus.WaitingForRequester, actor, AppRoles.SupportAgent, reason: null);
        });
    }

    [Fact]
    public async Task ValidateAndTransition_InProgressToResolved_WithoutSummary_ThrowsValidationException()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var req = new ServiceRequest { Id = 1, Status = RequestStatus.InProgress, RowVersion = Guid.NewGuid().ToByteArray() };
        var actor = new ApplicationUser { Id = "agent1", FullName = "Support Agent" };

        await Assert.ThrowsAsync<DomainValidationException>(async () =>
        {
            await sut.ValidateAndTransitionAsync(
                req,
                RequestStatus.Resolved,
                actor,
                AppRoles.SupportAgent,
                resolutionSummary: "",
                resolutionCategory: "Hardware");
        });
    }

    [Fact]
    public async Task ValidateAndTransition_ResolvedToInProgress_Within7Days_Succeeds()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var req = new ServiceRequest
        {
            Id = 1,
            Status = RequestStatus.Resolved,
            ResolvedAtUtc = DateTime.UtcNow.AddDays(-2),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        context.ServiceRequests.Add(req);
        await context.SaveChangesAsync();

        var actor = new ApplicationUser { Id = "req1", FullName = "Requester" };

        await sut.ValidateAndTransitionAsync(
            req,
            RequestStatus.InProgress,
            actor,
            AppRoles.Requester,
            reason: "Issue reoccurred after 2 days.");

        Assert.Equal(RequestStatus.InProgress, req.Status);
        Assert.Null(req.ResolvedAtUtc);
    }

    [Fact]
    public async Task ValidateAndTransition_ResolvedToInProgress_After7Days_ThrowsWorkflowException()
    {
        using var context = CreateInMemoryContext();
        var sut = new WorkflowService(context);

        var req = new ServiceRequest
        {
            Id = 1,
            Status = RequestStatus.Resolved,
            ResolvedAtUtc = DateTime.UtcNow.AddDays(-8), // 8 days ago!
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        var actor = new ApplicationUser { Id = "req1", FullName = "Requester" };

        var ex = await Assert.ThrowsAsync<WorkflowTransitionException>(async () =>
        {
            await sut.ValidateAndTransitionAsync(
                req,
                RequestStatus.InProgress,
                actor,
                AppRoles.Requester,
                reason: "Disputing resolution after 8 days.");
        });

        Assert.Contains("cannot be reopened after seven calendar days", ex.Message);
    }
}
