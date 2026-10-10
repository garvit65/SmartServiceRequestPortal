using ServicePortal.Application.Services;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using Xunit;

namespace ServicePortal.UnitTests;

public class SlaCalculationServiceTests
{
    private readonly SlaCalculationService _sut = new();

    [Fact]
    public void CalculateDueDate_WithinSameBusinessDay_AddsHoursCorrectly()
    {
        // Monday 10:00 UTC + 4 business hours = Monday 14:00 UTC
        var monday10Am = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        var due = _sut.CalculateDueDate(monday10Am, 4);

        Assert.Equal(new DateTime(2026, 10, 12, 14, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void CalculateDueDate_SpanningPastBusinessClose_RollsOverToNextMorning()
    {
        // Monday 16:00 UTC + 4 business hours:
        // 2 hours on Monday (16:00 - 18:00), remaining 2 hours on Tuesday (09:00 - 11:00)
        var monday4Pm = new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc);
        var due = _sut.CalculateDueDate(monday4Pm, 4);

        Assert.Equal(new DateTime(2026, 10, 13, 11, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void CalculateDueDate_OverWeekend_ExcludesSaturdayAndSunday()
    {
        // Friday 15:00 UTC + 5 business hours:
        // 3 hours on Friday (15:00 - 18:00), weekend skipped, 2 hours on Monday (09:00 - 11:00)
        var friday3Pm = new DateTime(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc);
        var due = _sut.CalculateDueDate(friday3Pm, 5);

        Assert.Equal(new DateTime(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc), due);
        Assert.Equal(DayOfWeek.Monday, due.DayOfWeek);
    }

    [Fact]
    public void CalculateDueDate_StartingOnWeekend_NormalizesToMondayMorning()
    {
        // Saturday 14:00 UTC normalized to Monday 09:00 UTC + 3 hours = Monday 12:00 UTC
        var saturday = new DateTime(2026, 10, 17, 14, 0, 0, DateTimeKind.Utc);
        var due = _sut.CalculateDueDate(saturday, 3);

        Assert.Equal(new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void EvaluateSlaStatus_WhenBeforeDeadline_ReturnsWithinSla()
    {
        var now = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        var request = new ServiceRequest
        {
            CreatedAtUtc = now.AddHours(-1),
            ResponseDueAtUtc = now.AddHours(3),
            ResolutionDueAtUtc = now.AddHours(20),
            Status = RequestStatus.InProgress
        };

        var status = _sut.EvaluateSlaStatus(request, now);
        Assert.Equal(SlaStatus.WithinSla, status);
    }

    [Fact]
    public void EvaluateSlaStatus_WhenApproachingThreshold_ReturnsApproachingBreach()
    {
        var start = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
        var due = start.AddHours(10);
        // At 8.5 hours elapsed (85% of total 10h), should flag approaching breach
        var now = start.AddHours(8.5);

        var request = new ServiceRequest
        {
            CreatedAtUtc = start,
            ResponseDueAtUtc = start.AddHours(2),
            FirstRespondedAtUtc = start.AddHours(1),
            ResolutionDueAtUtc = due,
            Status = RequestStatus.InProgress
        };

        var status = _sut.EvaluateSlaStatus(request, now);
        Assert.Equal(SlaStatus.ApproachingBreach, status);
    }

    [Fact]
    public void EvaluateSlaStatus_WhenPastResolutionDeadline_ReturnsBreached()
    {
        var now = new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);
        var request = new ServiceRequest
        {
            CreatedAtUtc = now.AddHours(-10),
            ResolutionDueAtUtc = now.AddHours(-1), // 1 hour past due!
            Status = RequestStatus.InProgress
        };

        var isBreached = _sut.IsBreached(request, now);
        var status = _sut.EvaluateSlaStatus(request, now);

        Assert.True(isBreached);
        Assert.Equal(SlaStatus.Breached, status);
    }
}
