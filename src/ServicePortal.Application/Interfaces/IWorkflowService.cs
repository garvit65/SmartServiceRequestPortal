using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Interfaces;

public interface IWorkflowService
{
    bool CanTransition(RequestStatus from, RequestStatus to, string userRole, out string errorMessage);
    Task ValidateAndTransitionAsync(
        ServiceRequest request,
        RequestStatus newStatus,
        ApplicationUser actor,
        string actorRole,
        string? reason = null,
        string? resolutionSummary = null,
        string? resolutionCategory = null,
        CancellationToken cancellationToken = default);
}
