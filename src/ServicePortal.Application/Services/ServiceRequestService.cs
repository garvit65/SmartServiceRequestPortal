using System.Text;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.DTOs;
using ServicePortal.Application.Exceptions;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly IApplicationDbContext _context;
    private readonly ISlaCalculationService _slaService;
    private readonly IWorkflowService _workflowService;
    private readonly IReferenceNumberGenerator _referenceGenerator;
    private readonly DuplicateRequestDetector _duplicateDetector;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IFileStorageService _fileStorageService;

    public ServiceRequestService(
        IApplicationDbContext context,
        ISlaCalculationService slaService,
        IWorkflowService workflowService,
        IReferenceNumberGenerator referenceGenerator,
        DuplicateRequestDetector duplicateDetector,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _slaService = slaService;
        _workflowService = workflowService;
        _referenceGenerator = referenceGenerator;
        _duplicateDetector = duplicateDetector;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _fileStorageService = fileStorageService;
    }

    public async Task<(bool Success, string? DuplicateWarning, ServiceRequest? Request)> CreateRequestAsync(
        CreateRequestDto dto,
        string requesterId,
        List<(Stream Stream, string FileName, string ContentType)> attachments,
        CancellationToken cancellationToken = default)
    {
        // Duplicate check if warning was not acknowledged
        if (!dto.WarningAcknowledged)
        {
            var (isDuplicate, existing) = await _duplicateDetector.CheckForDuplicateAsync(
                requesterId, dto.CategoryId, dto.Title, 24, cancellationToken);

            if (isDuplicate && existing != null)
            {
                var warningMsg = $"A similar active request '{existing.Title}' ({existing.ReferenceNumber}) in this category was submitted recently on {existing.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC. Do you still wish to submit?";
                return (false, warningMsg, null);
            }
        }

        var category = await _context.Categories
            .Include(c => c.SupportTeam)
            .FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.IsActive, cancellationToken);

        if (category == null)
        {
            throw new DomainValidationException("The selected category is invalid or inactive.");
        }

        var slaPolicy = await _context.SlaPolicies
            .FirstOrDefaultAsync(s => s.Priority == dto.Priority && s.IsActive, cancellationToken);

        var responseHours = slaPolicy?.ResponseTargetHours ?? (dto.Priority == RequestPriority.Critical ? 2 : 8);
        var resolutionHours = slaPolicy?.ResolutionTargetHours ?? (dto.Priority == RequestPriority.Critical ? 8 : 40);

        var nowUtc = DateTime.UtcNow;
        var responseDueUtc = _slaService.CalculateDueDate(nowUtc, responseHours);
        var resolutionDueUtc = _slaService.CalculateDueDate(nowUtc, resolutionHours);

        var referenceNumber = await _referenceGenerator.GenerateNextReferenceAsync(cancellationToken);

        var request = new ServiceRequest
        {
            ReferenceNumber = referenceNumber,
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            CategoryId = dto.CategoryId,
            Priority = dto.Priority,
            Status = RequestStatus.New,
            PreferredContactMethod = dto.PreferredContactMethod,
            RequesterId = requesterId,
            AssignedTeamId = category.SupportTeamId, // Automatic team routing based on category
            CreatedAtUtc = nowUtc,
            ResponseDueAtUtc = responseDueUtc,
            ResolutionDueAtUtc = resolutionDueUtc,
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        _context.ServiceRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        // Record Initial Status History
        var history = new RequestStatusHistory
        {
            ServiceRequestId = request.Id,
            FromStatus = RequestStatus.New,
            ToStatus = RequestStatus.New,
            ChangedById = requesterId,
            Reason = "Service request submitted by requester.",
            ChangedAtUtc = nowUtc
        };
        _context.RequestStatusHistories.Add(history);

        // Process attachments
        if (attachments != null && attachments.Any())
        {
            foreach (var att in attachments)
            {
                if (!_fileStorageService.IsAllowedExtension(att.FileName))
                {
                    continue; // Skip invalid extensions
                }

                var (storedName, size) = await _fileStorageService.SaveFileAsync(att.Stream, att.FileName, cancellationToken);
                var attachment = new RequestAttachment
                {
                    ServiceRequestId = request.Id,
                    OriginalFileName = att.FileName,
                    StoredFileName = storedName,
                    ContentType = att.ContentType,
                    FileSizeBytes = size,
                    UploadedById = requesterId,
                    UploadedAtUtc = nowUtc
                };
                _context.RequestAttachments.Add(attachment);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Audit Log and Notifications
        await _auditLogService.LogAsync(
            requesterId,
            "Requester",
            "CreateRequest",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Created request {request.ReferenceNumber} under category '{category.Name}' (Team: {category.SupportTeam?.Name}).",
            null,
            cancellationToken);

        return (true, null, request);
    }

    public async Task<RequestDetailsDto?> GetRequestDetailsAsync(
        int id,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.AssignedTeam)
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .Include(r => r.Comments).ThenInclude(c => c.Author)
            .Include(r => r.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(r => r.StatusHistory).ThenInclude(h => h.ChangedBy)
            .Include(r => r.AssignmentHistory).ThenInclude(a => a.NewAssignee)
            .Include(r => r.AssignmentHistory).ThenInclude(a => a.AssignedBy)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request == null)
        {
            return null;
        }

        // Authorization boundary checks (Prevent IDOR)
        if (userRole == AppRoles.Requester && request.RequesterId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this service request.");
        }

        if (userRole == AppRoles.SupportAgent)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (request.AssigneeId != userId && request.AssignedTeamId != user?.SupportTeamId)
            {
                throw new UnauthorizedAccessException("You are not authorized to view requests outside your assigned team or assignments.");
            }
        }

        if (userRole == AppRoles.TeamLead)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (request.AssignedTeamId != user?.SupportTeamId)
            {
                throw new UnauthorizedAccessException("You are not authorized to view requests belonging to another team.");
            }
        }

        var isRequester = (userRole == AppRoles.Requester);
        var nowUtc = DateTime.UtcNow;
        var slaStatus = _slaService.EvaluateSlaStatus(request, nowUtc);

        var dto = new RequestDetailsDto
        {
            Id = request.Id,
            ReferenceNumber = request.ReferenceNumber,
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            CategoryName = request.Category?.Name ?? "General",
            TeamId = request.AssignedTeamId,
            TeamName = request.AssignedTeam?.Name ?? "Unassigned",
            Priority = request.Priority,
            Status = request.Status,
            PreferredContactMethod = request.PreferredContactMethod,
            RequesterId = request.RequesterId,
            RequesterName = request.Requester?.FullName ?? request.Requester?.UserName ?? "Requester",
            RequesterEmail = request.Requester?.Email ?? string.Empty,
            AssigneeId = request.AssigneeId,
            AssigneeName = request.Assignee?.FullName ?? request.Assignee?.UserName,
            CreatedAtUtc = request.CreatedAtUtc,
            UpdatedAtUtc = request.UpdatedAtUtc,
            ResponseDueAtUtc = request.ResponseDueAtUtc,
            ResolutionDueAtUtc = request.ResolutionDueAtUtc,
            FirstRespondedAtUtc = request.FirstRespondedAtUtc,
            ResolvedAtUtc = request.ResolvedAtUtc,
            ClosedAtUtc = request.ClosedAtUtc,
            ResolutionSummary = request.ResolutionSummary,
            ResolutionCategory = request.ResolutionCategory,
            IsSlaBreached = request.IsSlaBreached,
            SlaStatus = slaStatus,
            RowVersion = request.RowVersion != null ? Convert.ToBase64String(request.RowVersion) : string.Empty,
            CanReopen = (request.Status == RequestStatus.Resolved &&
                         (isRequester || userRole == AppRoles.TeamLead || userRole == AppRoles.Administrator) &&
                         (!request.ResolvedAtUtc.HasValue || (nowUtc - request.ResolvedAtUtc.Value).TotalDays <= 7)),
            CanClose = (request.Status == RequestStatus.Resolved &&
                        (isRequester || userRole == AppRoles.TeamLead || userRole == AppRoles.Administrator))
        };

        // Comments filter: Requester NEVER sees internal comments!
        var visibleComments = request.Comments
            .Where(c => !isRequester || c.Visibility == CommentVisibility.Public)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new CommentItemDto
            {
                Id = c.Id,
                AuthorName = c.Author?.FullName ?? c.Author?.UserName ?? "User",
                AuthorRole = c.AuthorId == request.RequesterId ? "Requester" : "Support",
                IsInternal = c.Visibility == CommentVisibility.Internal,
                Body = c.Body,
                CreatedAtUtc = c.CreatedAtUtc
            }).ToList();

        dto.Comments = visibleComments;

        // Attachments
        dto.Attachments = request.Attachments
            .OrderBy(a => a.UploadedAtUtc)
            .Select(a => new AttachmentItemDto
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                StoredFileName = a.StoredFileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                UploadedByName = a.UploadedBy?.FullName ?? a.UploadedBy?.UserName ?? "User",
                UploadedAtUtc = a.UploadedAtUtc
            }).ToList();

        // Build Chronological Timeline
        var timeline = new List<ActivityTimelineItemDto>();

        foreach (var sh in request.StatusHistory)
        {
            timeline.Add(new ActivityTimelineItemDto
            {
                TimestampUtc = sh.ChangedAtUtc,
                EventType = "Status Change",
                ActorName = sh.ChangedBy?.FullName ?? "System",
                Details = $"Status changed from {sh.FromStatus} to {sh.ToStatus}. {(string.IsNullOrWhiteSpace(sh.Reason) ? "" : "Reason: " + sh.Reason)}",
                IsInternal = false
            });
        }

        foreach (var ah in request.AssignmentHistory)
        {
            timeline.Add(new ActivityTimelineItemDto
            {
                TimestampUtc = ah.AssignedAtUtc,
                EventType = "Assignment",
                ActorName = ah.AssignedBy?.FullName ?? "System",
                Details = $"Assigned to {ah.NewAssignee?.FullName ?? "Agent"}. {(string.IsNullOrWhiteSpace(ah.Reason) ? "" : "Reason: " + ah.Reason)}",
                IsInternal = false
            });
        }

        foreach (var c in visibleComments)
        {
            timeline.Add(new ActivityTimelineItemDto
            {
                TimestampUtc = c.CreatedAtUtc,
                EventType = c.IsInternal ? "Internal Note" : "Comment",
                ActorName = c.AuthorName,
                Details = c.Body,
                IsInternal = c.IsInternal
            });
        }

        foreach (var att in dto.Attachments)
        {
            timeline.Add(new ActivityTimelineItemDto
            {
                TimestampUtc = att.UploadedAtUtc,
                EventType = "Attachment Added",
                ActorName = att.UploadedByName,
                Details = $"Attached file: {att.OriginalFileName} ({(att.FileSizeBytes / 1024.0):F1} KB)",
                IsInternal = false
            });
        }

        dto.Timeline = timeline.OrderBy(t => t.TimestampUtc).ToList();

        return dto;
    }

    public async Task<PaginatedList<RequestListItemDto>> FilterRequestsAsync(
        RequestFilterDto filter,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.AssignedTeam)
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .AsQueryable();

        // Enforce user role access scope
        if (userRole == AppRoles.Requester)
        {
            query = query.Where(r => r.RequesterId == userId);
        }
        else if (userRole == AppRoles.SupportAgent)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            var teamId = user?.SupportTeamId;
            query = query.Where(r => r.AssigneeId == userId || (teamId.HasValue && r.AssignedTeamId == teamId.Value));
        }
        else if (userRole == AppRoles.TeamLead)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            var teamId = user?.SupportTeamId;
            if (teamId.HasValue)
            {
                query = query.Where(r => r.AssignedTeamId == teamId.Value);
            }
        }

        // Apply filters
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.ReferenceNumber.ToLower().Contains(term) ||
                r.Title.ToLower().Contains(term) ||
                r.Description.ToLower().Contains(term) ||
                (r.Requester != null && (r.Requester.FullName.ToLower().Contains(term) || r.Requester.Email!.ToLower().Contains(term))));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(r => r.CategoryId == filter.CategoryId.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(r => r.Priority == filter.Priority.Value);
        }

        if (filter.TeamId.HasValue)
        {
            query = query.Where(r => r.AssignedTeamId == filter.TeamId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.AssigneeId))
        {
            query = query.Where(r => r.AssigneeId == filter.AssigneeId);
        }

        if (!string.IsNullOrWhiteSpace(filter.RequesterId))
        {
            query = query.Where(r => r.RequesterId == filter.RequesterId);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(r => r.CreatedAtUtc >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(r => r.CreatedAtUtc <= filter.EndDate.Value);
        }

        // Sorting
        query = filter.SortBy switch
        {
            "Reference" => filter.SortDescending ? query.OrderByDescending(r => r.ReferenceNumber) : query.OrderBy(r => r.ReferenceNumber),
            "Title" => filter.SortDescending ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
            "Priority" => filter.SortDescending ? query.OrderByDescending(r => r.Priority) : query.OrderBy(r => r.Priority),
            "Status" => filter.SortDescending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "ResolutionDue" => filter.SortDescending ? query.OrderByDescending(r => r.ResolutionDueAtUtc) : query.OrderBy(r => r.ResolutionDueAtUtc),
            _ => filter.SortDescending ? query.OrderByDescending(r => r.CreatedAtUtc) : query.OrderBy(r => r.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var pageIndex = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 5, 100);

        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;

        var dtos = items.Select(r => new RequestListItemDto
        {
            Id = r.Id,
            ReferenceNumber = r.ReferenceNumber,
            Title = r.Title,
            CategoryName = r.Category?.Name ?? "General",
            TeamName = r.AssignedTeam?.Name ?? "Unassigned",
            Priority = r.Priority,
            Status = r.Status,
            RequesterId = r.RequesterId,
            RequesterName = r.Requester?.FullName ?? r.Requester?.UserName ?? "Requester",
            RequesterEmail = r.Requester?.Email ?? string.Empty,
            AssigneeId = r.AssigneeId,
            AssigneeName = r.Assignee?.FullName ?? r.Assignee?.UserName,
            CreatedAtUtc = r.CreatedAtUtc,
            ResponseDueAtUtc = r.ResponseDueAtUtc,
            ResolutionDueAtUtc = r.ResolutionDueAtUtc,
            IsSlaBreached = r.IsSlaBreached || _slaService.IsBreached(r, nowUtc),
            SlaStatus = _slaService.EvaluateSlaStatus(r, nowUtc)
        }).ToList();

        // Filter by SLA state if requested
        if (filter.SlaState.HasValue)
        {
            dtos = dtos.Where(d => d.SlaStatus == filter.SlaState.Value).ToList();
        }

        return PaginatedList<RequestListItemDto>.Create(dtos, totalCount, pageIndex, pageSize);
    }

    public async Task AssignRequestAsync(
        AssignRequestDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.TeamLead && actorRole != AppRoles.Administrator)
        {
            throw new UnauthorizedAccessException("Only Team Leads or Administrators are authorized to assign requests.");
        }

        var request = await _context.ServiceRequests
            .Include(r => r.AssignedTeam)
            .FirstOrDefaultAsync(r => r.Id == dto.RequestId, cancellationToken);

        if (request == null)
        {
            throw new NotFoundException(nameof(ServiceRequest), dto.RequestId);
        }

        ValidateRowVersion(request, dto.RowVersion);

        var assignee = await _context.Users.FindAsync(new object[] { dto.AssigneeId }, cancellationToken);
        if (assignee == null || !assignee.IsActive)
        {
            throw new DomainValidationException("The selected assignee is invalid or inactive.");
        }

        // Team Lead can only assign to active members of their team
        if (actorRole == AppRoles.TeamLead)
        {
            var lead = await _context.Users.FindAsync(new object[] { actorId }, cancellationToken);
            if (request.AssignedTeamId != lead?.SupportTeamId)
            {
                throw new UnauthorizedAccessException("Team Leads can only assign requests belonging to their own team.");
            }
            if (assignee.SupportTeamId != lead?.SupportTeamId)
            {
                throw new DomainValidationException("You can only assign requests to members of your own support team.");
            }
        }

        var prevAssigneeId = request.AssigneeId;
        request.AssigneeId = assignee.Id;
        request.UpdatedAtUtc = DateTime.UtcNow;
        request.RowVersion = Guid.NewGuid().ToByteArray();

        // Record Assignment History
        var assignmentHistory = new AssignmentHistory
        {
            ServiceRequestId = request.Id,
            PreviousAssigneeId = prevAssigneeId,
            NewAssigneeId = assignee.Id,
            AssignedById = actorId,
            Reason = dto.Reason,
            AssignedAtUtc = DateTime.UtcNow
        };
        _context.AssignmentHistories.Add(assignmentHistory);

        // If request was New, transition to Assigned
        if (request.Status == RequestStatus.New)
        {
            var actor = await _context.Users.FindAsync(new object[] { actorId }, cancellationToken);
            if (actor != null)
            {
                await _workflowService.ValidateAndTransitionAsync(
                    request,
                    RequestStatus.Assigned,
                    actor,
                    actorRole,
                    dto.Reason ?? $"Assigned to {assignee.FullName}",
                    null,
                    null,
                    cancellationToken);
            }
        }
        else
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Notifications & Audit Log
        await _notificationService.NotifyRequestAssignedAsync(request, assignee, cancellationToken);
        await _auditLogService.LogAsync(
            actorId,
            actorRole,
            "AssignRequest",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Assigned request {request.ReferenceNumber} to {assignee.FullName}.",
            null,
            cancellationToken);
    }

    public async Task AddCommentAsync(
        AddCommentDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests.FindAsync(new object[] { dto.RequestId }, cancellationToken);
        if (request == null)
        {
            throw new NotFoundException(nameof(ServiceRequest), dto.RequestId);
        }

        if (request.Status == RequestStatus.Closed && actorRole != AppRoles.Administrator)
        {
            throw new DomainValidationException("Comments cannot be added to closed requests.");
        }

        // Requesters can NEVER create internal comments
        if (actorRole == AppRoles.Requester && dto.IsInternal)
        {
            throw new UnauthorizedAccessException("Requesters cannot create internal comments.");
        }

        var comment = new RequestComment
        {
            ServiceRequestId = request.Id,
            AuthorId = actorId,
            Visibility = dto.IsInternal ? CommentVisibility.Internal : CommentVisibility.Public,
            Body = dto.Body.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.RequestComments.Add(comment);
        request.UpdatedAtUtc = DateTime.UtcNow;

        // If request was WaitingForRequester and Requester comments, resume to InProgress
        if (request.Status == RequestStatus.WaitingForRequester && actorRole == AppRoles.Requester)
        {
            var actor = await _context.Users.FindAsync(new object[] { actorId }, cancellationToken);
            if (actor != null)
            {
                await _workflowService.ValidateAndTransitionAsync(
                    request,
                    RequestStatus.InProgress,
                    actor,
                    actorRole,
                    "Requester replied with additional information.",
                    null,
                    null,
                    cancellationToken);
            }
        }
        else
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        await _notificationService.NotifyCommentAddedAsync(request, comment, cancellationToken);
        await _auditLogService.LogAsync(
            actorId,
            actorRole,
            "AddComment",
            nameof(RequestComment),
            comment.Id.ToString(),
            $"Added {(dto.IsInternal ? "internal" : "public")} comment to request {request.ReferenceNumber}.",
            null,
            cancellationToken);
    }

    public async Task ResolveRequestAsync(
        ResolveRequestDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests.FindAsync(new object[] { dto.RequestId }, cancellationToken);
        if (request == null)
        {
            throw new NotFoundException(nameof(ServiceRequest), dto.RequestId);
        }

        ValidateRowVersion(request, dto.RowVersion);

        var actor = await _context.Users.FindAsync(new object[] { actorId }, cancellationToken);
        if (actor == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), actorId);
        }

        await _workflowService.ValidateAndTransitionAsync(
            request,
            RequestStatus.Resolved,
            actor,
            actorRole,
            dto.ResolutionSummary,
            dto.ResolutionSummary,
            dto.ResolutionCategory,
            cancellationToken);

        request.RowVersion = Guid.NewGuid().ToByteArray();
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyStatusChangedAsync(request, actor.FullName, cancellationToken);
        await _auditLogService.LogAsync(
            actorId,
            actorRole,
            "ResolveRequest",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Resolved request {request.ReferenceNumber} (Category: {dto.ResolutionCategory}).",
            null,
            cancellationToken);
    }

    public async Task ReopenRequestAsync(
        ReopenRequestDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests.FindAsync(new object[] { dto.RequestId }, cancellationToken);
        if (request == null)
        {
            throw new NotFoundException(nameof(ServiceRequest), dto.RequestId);
        }

        ValidateRowVersion(request, dto.RowVersion);

        var actor = await _context.Users.FindAsync(new object[] { actorId }, cancellationToken);
        if (actor == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), actorId);
        }

        await _workflowService.ValidateAndTransitionAsync(
            request,
            RequestStatus.InProgress,
            actor,
            actorRole,
            dto.Reason,
            null,
            null,
            cancellationToken);

        request.RowVersion = Guid.NewGuid().ToByteArray();
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyStatusChangedAsync(request, actor.FullName, cancellationToken);
        await _auditLogService.LogAsync(
            actorId,
            actorRole,
            "ReopenRequest",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Reopened request {request.ReferenceNumber}. Reason: {dto.Reason}",
            null,
            cancellationToken);
    }

    public async Task CloseRequestAsync(
        int requestId,
        string rowVersion,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests.FindAsync(new object[] { requestId }, cancellationToken);
        if (request == null)
        {
            throw new NotFoundException(nameof(ServiceRequest), requestId);
        }

        ValidateRowVersion(request, rowVersion);

        var actor = await _context.Users.FindAsync(new object[] { actorId }, cancellationToken);
        if (actor == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), actorId);
        }

        await _workflowService.ValidateAndTransitionAsync(
            request,
            RequestStatus.Closed,
            actor,
            actorRole,
            "Closed by user confirmation.",
            null,
            null,
            cancellationToken);

        request.RowVersion = Guid.NewGuid().ToByteArray();
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyStatusChangedAsync(request, actor.FullName, cancellationToken);
        await _auditLogService.LogAsync(
            actorId,
            actorRole,
            "CloseRequest",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Closed request {request.ReferenceNumber}.",
            null,
            cancellationToken);
    }

    public async Task<byte[]> ExportCsvAsync(
        RequestFilterDto filter,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        filter.PageNumber = 1;
        filter.PageSize = 5000; // Export up to 5000 matching records
        var paged = await FilterRequestsAsync(filter, userId, userRole, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("ReferenceNumber,Title,Category,Team,Priority,Status,RequesterName,RequesterEmail,AssigneeName,CreatedAtUtc,ResolutionDueAtUtc,SlaStatus");

        foreach (var r in paged.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(r.ReferenceNumber),
                EscapeCsv(r.Title),
                EscapeCsv(r.CategoryName),
                EscapeCsv(r.TeamName),
                EscapeCsv(r.Priority.ToString()),
                EscapeCsv(r.Status.ToString()),
                EscapeCsv(r.RequesterName),
                EscapeCsv(r.RequesterEmail),
                EscapeCsv(r.AssigneeName ?? "Unassigned"),
                EscapeCsv(r.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsv(r.ResolutionDueAtUtc.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsv(r.SlaStatus.ToString())
            ));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync(
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var filter = new RequestFilterDto { PageSize = 5 };
        var list = await FilterRequestsAsync(filter, userId, userRole, cancellationToken);

        var query = _context.ServiceRequests.AsQueryable();

        if (userRole == AppRoles.Requester)
        {
            query = query.Where(r => r.RequesterId == userId);
        }
        else if (userRole == AppRoles.SupportAgent)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            query = query.Where(r => r.AssigneeId == userId || (user != null && r.AssignedTeamId == user.SupportTeamId));
        }
        else if (userRole == AppRoles.TeamLead)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user?.SupportTeamId != null)
            {
                query = query.Where(r => r.AssignedTeamId == user.SupportTeamId);
            }
        }

        var allMatching = await query
            .Include(r => r.Category)
            .Include(r => r.Assignee)
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;

        var metrics = new DashboardMetricsDto
        {
            UserRole = userRole,
            TotalCount = allMatching.Count,
            OpenCount = allMatching.Count(r => r.Status == RequestStatus.New || r.Status == RequestStatus.Assigned),
            InProgressCount = allMatching.Count(r => r.Status == RequestStatus.InProgress),
            WaitingForRequesterCount = allMatching.Count(r => r.Status == RequestStatus.WaitingForRequester),
            ResolvedCount = allMatching.Count(r => r.Status == RequestStatus.Resolved),
            ClosedCount = allMatching.Count(r => r.Status == RequestStatus.Closed),
            BreachedCount = allMatching.Count(r => r.IsSlaBreached || _slaService.IsBreached(r, nowUtc)),
            DueSoonCount = allMatching.Count(r => _slaService.IsApproachingBreach(r, nowUtc)),
            RecentRequests = list.Items.Take(5).ToList()
        };

        // Compliance percentage
        if (metrics.TotalCount > 0)
        {
            metrics.SlaCompliancePercentage = Math.Round((metrics.TotalCount - metrics.BreachedCount) / (double)metrics.TotalCount * 100.0, 1);
        }
        else
        {
            metrics.SlaCompliancePercentage = 100.0;
        }

        // Average resolution hours
        var resolvedRequests = allMatching.Where(r => r.ResolvedAtUtc.HasValue).ToList();
        if (resolvedRequests.Any())
        {
            metrics.AverageResolutionHours = Math.Round(resolvedRequests.Average(r => (r.ResolvedAtUtc!.Value - r.CreatedAtUtc).TotalHours), 1);
        }

        // Status distribution
        metrics.StatusDistribution = allMatching
            .GroupBy(r => r.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Priority distribution
        metrics.PriorityDistribution = allMatching
            .GroupBy(r => r.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Category volume
        metrics.CategoryVolume = allMatching
            .GroupBy(r => r.Category?.Name ?? "General")
            .ToDictionary(g => g.Key, g => g.Count());

        // Agent workload
        metrics.AgentWorkload = allMatching
            .Where(r => r.Assignee != null && r.Status != RequestStatus.Closed)
            .GroupBy(r => r.Assignee!.FullName)
            .ToDictionary(g => g.Key, g => g.Count());

        return metrics;
    }

    private static void ValidateRowVersion(ServiceRequest request, string? clientRowVersion)
    {
        if (string.IsNullOrWhiteSpace(clientRowVersion) || request.RowVersion == null)
        {
            return;
        }

        var clientBytes = Convert.FromBase64String(clientRowVersion);
        if (!clientBytes.SequenceEqual(request.RowVersion))
        {
            throw new ConcurrencyConflictException("This service request was updated by another user while you were viewing it. Please reload the page to see the latest changes.");
        }
    }

    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "\"\"";
        }

        var escaped = field.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
