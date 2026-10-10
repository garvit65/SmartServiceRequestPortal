using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Interfaces;

public interface ISlaCalculationService
{
    DateTime CalculateDueDate(DateTime startUtc, int targetBusinessHours);
    SlaStatus EvaluateSlaStatus(ServiceRequest request, DateTime nowUtc);
    bool IsBreached(ServiceRequest request, DateTime nowUtc);
    bool IsApproachingBreach(ServiceRequest request, DateTime nowUtc, double warningThresholdPercent = 0.80);
}
