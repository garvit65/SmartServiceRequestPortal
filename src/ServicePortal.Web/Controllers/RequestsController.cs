using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.DTOs;
using ServicePortal.Application.Exceptions;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Web.Controllers;

[Authorize]
public class RequestsController : Controller
{
    private readonly IServiceRequestService _requestService;
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(
        IServiceRequestService requestService,
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        ILogger<RequestsController> logger)
    {
        _requestService = requestService;
        _context = context;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(RequestFilterDto filter)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        var pagedRequests = await _requestService.FilterRequestsAsync(filter, userId, userRole);

        // Populate dropdown options
        await PopulateFilterOptionsAsync(filter);

        ViewBag.Filter = filter;
        return View(pagedRequests);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        try
        {
            var details = await _requestService.GetRequestDetailsAsync(id, userId, userRole);
            if (details == null)
            {
                return NotFound();
            }

            // Populate potential assignees if user is Team Lead or Admin
            if (userRole == AppRoles.TeamLead || userRole == AppRoles.Administrator)
            {
                var query = _context.Users.Where(u => u.IsActive);
                if (userRole == AppRoles.TeamLead && details.TeamId.HasValue)
                {
                    query = query.Where(u => u.SupportTeamId == details.TeamId.Value);
                }
                var agents = await query.ToListAsync();
                ViewBag.AvailableAssignees = new SelectList(agents, "Id", "FullName", details.AssigneeId);
            }

            return View(details);
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Index");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Categories = new SelectList(categories, "Id", "Name");
        return View(new CreateRequestDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRequestDto dto, List<IFormFile> attachments)
    {
        var userId = GetCurrentUserId();

        if (!ModelState.IsValid)
        {
            var categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", dto.CategoryId);
            return View(dto);
        }

        var fileStreams = new List<(Stream Stream, string FileName, string ContentType)>();
        try
        {
            if (attachments != null)
            {
                foreach (var file in attachments)
                {
                    if (file.Length > 0)
                    {
                        var stream = new MemoryStream();
                        await file.CopyToAsync(stream);
                        stream.Position = 0;
                        fileStreams.Add((stream, file.FileName, file.ContentType));
                    }
                }
            }

            var (success, warning, request) = await _requestService.CreateRequestAsync(dto, userId, fileStreams);

            if (!success && !string.IsNullOrEmpty(warning))
            {
                // Duplicate warning detected: redisplay form asking user to acknowledge
                ViewBag.DuplicateWarning = warning;
                var categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name", dto.CategoryId);
                return View(dto);
            }

            TempData["SuccessMessage"] = $"Service Request {request!.ReferenceNumber} has been successfully submitted!";
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service request.");
            ModelState.AddModelError(string.Empty, ex.Message);
            var categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", dto.CategoryId);
            return View(dto);
        }
        finally
        {
            foreach (var item in fileStreams)
            {
                item.Stream.Dispose();
            }
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(AssignRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        try
        {
            await _requestService.AssignRequestAsync(dto, userId, userRole);
            TempData["SuccessMessage"] = "Request successfully assigned.";
        }
        catch (ConcurrencyConflictException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = dto.RequestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransitionStatus(
        int requestId,
        RequestStatus newStatus,
        string rowVersion,
        string? reason = null,
        string? resolutionSummary = null,
        string? resolutionCategory = null)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        try
        {
            switch (newStatus)
            {
                case RequestStatus.Resolved:
                    await _requestService.ResolveRequestAsync(new ResolveRequestDto
                    {
                        RequestId = requestId,
                        ResolutionSummary = resolutionSummary ?? string.Empty,
                        ResolutionCategory = resolutionCategory ?? "General",
                        RowVersion = rowVersion
                    }, userId, userRole);
                    TempData["SuccessMessage"] = "Request has been marked as Resolved.";
                    break;

                case RequestStatus.InProgress when reason != null && reason.Contains("reopen", StringComparison.OrdinalIgnoreCase):
                    await _requestService.ReopenRequestAsync(new ReopenRequestDto
                    {
                        RequestId = requestId,
                        Reason = reason,
                        RowVersion = rowVersion
                    }, userId, userRole);
                    TempData["SuccessMessage"] = "Request has been reopened.";
                    break;

                case RequestStatus.Closed:
                    await _requestService.CloseRequestAsync(requestId, rowVersion, userId, userRole);
                    TempData["SuccessMessage"] = "Request has been confirmed and closed.";
                    break;

                default:
                    // Regular status transitions (InProgress, WaitingForRequester)
                    var request = await _context.ServiceRequests.FindAsync(requestId);
                    if (request == null) return NotFound();

                    var actor = await _context.Users.FindAsync(userId);
                    var workflow = HttpContext.RequestServices.GetRequiredService<IWorkflowService>();
                    await workflow.ValidateAndTransitionAsync(request, newStatus, actor!, userRole, reason);
                    TempData["SuccessMessage"] = $"Request status moved to '{newStatus}'.";
                    break;
            }
        }
        catch (ConcurrencyConflictException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = requestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(AddCommentDto dto)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        try
        {
            await _requestService.AddCommentAsync(dto, userId, userRole);
            TempData["SuccessMessage"] = dto.IsInternal ? "Internal note added." : "Public comment posted.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = dto.RequestId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        var attachment = await _context.RequestAttachments
            .Include(a => a.ServiceRequest)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment == null)
        {
            return NotFound();
        }

        // Check permission (AC-02)
        if (userRole == AppRoles.Requester && attachment.ServiceRequest?.RequesterId != userId)
        {
            return Forbid();
        }

        try
        {
            var stream = await _fileStorageService.GetFileStreamAsync(attachment.StoredFileName);
            return File(stream, attachment.ContentType, attachment.OriginalFileName);
        }
        catch (FileNotFoundException)
        {
            TempData["ErrorMessage"] = "Attachment file was not found on the server storage.";
            return RedirectToAction(nameof(Details), new { id = attachment.ServiceRequestId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(RequestFilterDto filter)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        var csvBytes = await _requestService.ExportCsvAsync(filter, userId, userRole);
        var fileName = $"ServiceRequests_Export_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }

    private string GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private string GetCurrentUserRole() =>
        User.IsInRole(AppRoles.Administrator) ? AppRoles.Administrator
        : User.IsInRole(AppRoles.TeamLead) ? AppRoles.TeamLead
        : User.IsInRole(AppRoles.SupportAgent) ? AppRoles.SupportAgent
        : AppRoles.Requester;

    private async Task PopulateFilterOptionsAsync(RequestFilterDto filter)
    {
        ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.IsActive).ToListAsync(), "Id", "Name", filter.CategoryId);
        ViewBag.Teams = new SelectList(await _context.SupportTeams.Where(t => t.IsActive).ToListAsync(), "Id", "Name", filter.TeamId);
    }
}
