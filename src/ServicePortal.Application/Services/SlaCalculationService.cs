using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Services;

public class SlaCalculationService : ISlaCalculationService
{
    // Contoso Support Services business hours: Monday to Friday, 09:00 to 18:00 (9 business hours per day)
    public const int BusinessDayStartHour = 9;
    public const int BusinessDayEndHour = 18;
    public const int BusinessHoursPerDay = BusinessDayEndHour - BusinessDayStartHour; // 9 hours

    public DateTime CalculateDueDate(DateTime startUtc, int targetBusinessHours)
    {
        if (targetBusinessHours <= 0)
        {
            return startUtc;
        }

        var current = startUtc;

        // Normalize start time to the next valid business moment if outside business hours or weekend
        current = NormalizeToBusinessTime(current);

        var remainingHours = (double)targetBusinessHours;

        while (remainingHours > 0)
        {
            var endOfCurrentBusinessDay = new DateTime(current.Year, current.Month, current.Day, BusinessDayEndHour, 0, 0, DateTimeKind.Utc);
            var availableHoursToday = (endOfCurrentBusinessDay - current).TotalHours;

            if (remainingHours <= availableHoursToday)
            {
                current = current.AddHours(remainingHours);
                remainingHours = 0;
            }
            else
            {
                remainingHours -= availableHoursToday;
                // Move to start of next business day
                current = GetNextBusinessDayStart(current.Date.AddDays(1));
            }
        }

        return current;
    }

    private static DateTime NormalizeToBusinessTime(DateTime dt)
    {
        // Skip weekend to Monday morning
        while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
        {
            dt = dt.Date.AddDays(1);
            dt = new DateTime(dt.Year, dt.Month, dt.Day, BusinessDayStartHour, 0, 0, DateTimeKind.Utc);
        }

        if (dt.Hour < BusinessDayStartHour)
        {
            dt = new DateTime(dt.Year, dt.Month, dt.Day, BusinessDayStartHour, 0, 0, DateTimeKind.Utc);
        }
        else if (dt.Hour >= BusinessDayEndHour)
        {
            dt = GetNextBusinessDayStart(dt.Date.AddDays(1));
        }

        return dt;
    }

    private static DateTime GetNextBusinessDayStart(DateTime dt)
    {
        while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
        {
            dt = dt.AddDays(1);
        }
        return new DateTime(dt.Year, dt.Month, dt.Day, BusinessDayStartHour, 0, 0, DateTimeKind.Utc);
    }

    public SlaStatus EvaluateSlaStatus(ServiceRequest request, DateTime nowUtc)
    {
        if (request.Status == RequestStatus.Closed)
        {
            return request.IsSlaBreached ? SlaStatus.Breached : SlaStatus.WithinSla;
        }

        if (IsBreached(request, nowUtc))
        {
            return SlaStatus.Breached;
        }

        if (IsApproachingBreach(request, nowUtc))
        {
            return SlaStatus.ApproachingBreach;
        }

        return SlaStatus.WithinSla;
    }

    public bool IsBreached(ServiceRequest request, DateTime nowUtc)
    {
        if (request.IsSlaBreached)
        {
            return true;
        }

        // Response breach check: if not yet responded and nowUtc > ResponseDueAtUtc
        if (request.FirstRespondedAtUtc == null &&
            (request.Status == RequestStatus.New || request.Status == RequestStatus.Assigned) &&
            nowUtc > request.ResponseDueAtUtc)
        {
            return true;
        }

        // Resolution breach check: if not yet resolved and nowUtc > ResolutionDueAtUtc
        if (request.ResolvedAtUtc == null &&
            request.Status != RequestStatus.Resolved &&
            request.Status != RequestStatus.Closed &&
            nowUtc > request.ResolutionDueAtUtc)
        {
            return true;
        }

        // Check if resolved after due date
        if (request.ResolvedAtUtc.HasValue && request.ResolvedAtUtc.Value > request.ResolutionDueAtUtc)
        {
            return true;
        }

        return false;
    }

    public bool IsApproachingBreach(ServiceRequest request, DateTime nowUtc, double warningThresholdPercent = 0.80)
    {
        if (request.Status == RequestStatus.Resolved || request.Status == RequestStatus.Closed)
        {
            return false;
        }

        // Check Response SLA threshold
        if (request.FirstRespondedAtUtc == null && (request.Status == RequestStatus.New || request.Status == RequestStatus.Assigned))
        {
            var totalResponseSpan = (request.ResponseDueAtUtc - request.CreatedAtUtc).TotalMinutes;
            if (totalResponseSpan > 0)
            {
                var elapsed = (nowUtc - request.CreatedAtUtc).TotalMinutes;
                if (elapsed >= totalResponseSpan * warningThresholdPercent)
                {
                    return true;
                }
            }
        }

        // Check Resolution SLA threshold
        var totalResolutionSpan = (request.ResolutionDueAtUtc - request.CreatedAtUtc).TotalMinutes;
        if (totalResolutionSpan > 0)
        {
            var elapsed = (nowUtc - request.CreatedAtUtc).TotalMinutes;
            if (elapsed >= totalResolutionSpan * warningThresholdPercent)
            {
                return true;
            }
        }

        return false;
    }
}
