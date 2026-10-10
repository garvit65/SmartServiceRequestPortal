using ServicePortal.Application.Exceptions;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IApplicationDbContext _context;

    public WorkflowService(IApplicationDbContext context)
    {
        _context = context;
    }

    public bool CanTransition(RequestStatus from, RequestStatus to, string userRole, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (from == to)
        {
            errorMessage = $"Request is already in status '{from}'.";
            return false;
        }

        switch (from)
        {
            case RequestStatus.New:
                if (to == RequestStatus.Assigned)
                {
                    if (userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only Team Leads or Administrators can assign requests.";
                        return false;
                    }
                    return true;
                }
                errorMessage = $"Cannot transition directly from '{from}' to '{to}'. Requests must first be Assigned.";
                return false;

            case RequestStatus.Assigned:
                if (to == RequestStatus.InProgress)
                {
                    if (userRole != AppRoles.SupportAgent && userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only Support Agents, Team Leads, or Administrators can start progress.";
                        return false;
                    }
                    return true;
                }
                if (to == RequestStatus.Assigned)
                {
                    if (userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only Team Leads or Administrators can reassign requests.";
                        return false;
                    }
                    return true;
                }
                errorMessage = $"Invalid transition from '{from}' to '{to}'.";
                return false;

            case RequestStatus.InProgress:
                if (to == RequestStatus.WaitingForRequester)
                {
                    if (userRole != AppRoles.SupportAgent && userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only Support Agents, Team Leads, or Administrators can change status to Waiting for Requester.";
                        return false;
                    }
                    return true;
                }
                if (to == RequestStatus.Resolved)
                {
                    if (userRole != AppRoles.SupportAgent && userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only Support Agents, Team Leads, or Administrators can resolve requests.";
                        return false;
                    }
                    return true;
                }
                if (to == RequestStatus.Assigned)
                {
                    if (userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only Team Leads or Administrators can reassign an In-Progress request.";
                        return false;
                    }
                    return true;
                }
                errorMessage = $"Invalid transition from '{from}' to '{to}'.";
                return false;

            case RequestStatus.WaitingForRequester:
                if (to == RequestStatus.InProgress)
                {
                    return true;
                }
                if (to == RequestStatus.Resolved)
                {
                    if (userRole != AppRoles.SupportAgent && userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only Support Agents or Leads can resolve a request waiting for requester.";
                        return false;
                    }
                    return true;
                }
                errorMessage = $"Invalid transition from '{from}' to '{to}'.";
                return false;

            case RequestStatus.Resolved:
                if (to == RequestStatus.Closed)
                {
                    // Requester, TeamLead, Admin, or automated process can close
                    return true;
                }
                if (to == RequestStatus.InProgress) // Reopen
                {
                    if (userRole != AppRoles.Requester && userRole != AppRoles.TeamLead && userRole != AppRoles.Administrator)
                    {
                        errorMessage = "Only the Requester, Team Lead, or Administrator can reopen a resolved request.";
                        return false;
                    }
                    return true;
                }
                errorMessage = $"Invalid transition from '{from}' to '{to}'.";
                return false;

            case RequestStatus.Closed:
                if (userRole == AppRoles.Administrator)
                {
                    errorMessage = "Closed requests cannot have their status modified. Only administrator correction notes are permitted.";
                }
                else
                {
                    errorMessage = "Closed requests are final and cannot be modified.";
                }
                return false;

            default:
                errorMessage = $"Unsupported request status '{from}'.";
                return false;
        }
    }

    public async Task ValidateAndTransitionAsync(
        ServiceRequest request,
        RequestStatus newStatus,
        ApplicationUser actor,
        string actorRole,
        string? reason = null,
        string? resolutionSummary = null,
        string? resolutionCategory = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Status == RequestStatus.Closed && actorRole != AppRoles.Administrator)
        {
            throw new WorkflowTransitionException("Closed requests cannot be modified.");
        }

        if (!CanTransition(request.Status, newStatus, actorRole, out var error))
        {
            throw new WorkflowTransitionException(error);
        }

        // Additional business rule validations
        if (newStatus == RequestStatus.WaitingForRequester)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainValidationException("Moving to 'Waiting for Requester' requires an explanation of what information is needed.");
            }
        }

        if (newStatus == RequestStatus.Resolved)
        {
            if (string.IsNullOrWhiteSpace(resolutionSummary))
            {
                throw new DomainValidationException("Resolving a request requires a resolution summary.");
            }
            if (string.IsNullOrWhiteSpace(resolutionCategory))
            {
                throw new DomainValidationException("Resolving a request requires a resolution category.");
            }
            request.ResolutionSummary = resolutionSummary;
            request.ResolutionCategory = resolutionCategory;
            request.ResolvedAtUtc = DateTime.UtcNow;
        }

        // If reopening from Resolved to InProgress
        if (request.Status == RequestStatus.Resolved && newStatus == RequestStatus.InProgress)
        {
            if (request.ResolvedAtUtc.HasValue)
            {
                var daysSinceResolution = (DateTime.UtcNow - request.ResolvedAtUtc.Value).TotalDays;
                if (daysSinceResolution > 7)
                {
                    throw new WorkflowTransitionException("A request cannot be reopened after seven calendar days have elapsed.");
                }
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainValidationException("Reopening a request requires a reason.");
            }

            // Clear resolution data when reopening
            request.ResolvedAtUtc = null;
        }

        if (newStatus == RequestStatus.Closed)
        {
            request.ClosedAtUtc = DateTime.UtcNow;
        }

        // Capture first response if transition indicates first action by support
        if (request.FirstRespondedAtUtc == null &&
            (newStatus == RequestStatus.InProgress || newStatus == RequestStatus.WaitingForRequester || newStatus == RequestStatus.Resolved))
        {
            request.FirstRespondedAtUtc = DateTime.UtcNow;
        }

        var oldStatus = request.Status;
        request.Status = newStatus;
        request.UpdatedAtUtc = DateTime.UtcNow;

        // Record status history
        var history = new RequestStatusHistory
        {
            ServiceRequestId = request.Id,
            FromStatus = oldStatus,
            ToStatus = newStatus,
            ChangedById = actor.Id,
            Reason = reason,
            ChangedAtUtc = DateTime.UtcNow
        };

        _context.RequestStatusHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
