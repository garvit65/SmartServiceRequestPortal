using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Application.Interfaces;
using ServicePortal.Application.Services;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Web.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly IServiceRequestService _requestService;
    private readonly DuplicateRequestDetector _duplicateDetector;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IServiceRequestService requestService,
        DuplicateRequestDetector duplicateDetector,
        ILogger<ApiController> logger)
    {
        _requestService = requestService;
        _duplicateDetector = duplicateDetector;
        _logger = logger;
    }

    [HttpGet("requests/{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetRequestDetails(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.IsInRole(AppRoles.Administrator) ? AppRoles.Administrator
            : User.IsInRole(AppRoles.TeamLead) ? AppRoles.TeamLead
            : User.IsInRole(AppRoles.SupportAgent) ? AppRoles.SupportAgent
            : AppRoles.Requester;

        var details = await _requestService.GetRequestDetailsAsync(id, userId, userRole);
        if (details == null)
        {
            return NotFound(new { message = $"Service request with ID {id} was not found." });
        }

        return Ok(details);
    }

    [HttpGet("check-duplicate")]
    [Authorize]
    public async Task<IActionResult> CheckDuplicate([FromQuery] int categoryId, [FromQuery] string title)
    {
        if (string.IsNullOrWhiteSpace(title) || categoryId <= 0)
        {
            return Ok(new { isDuplicate = false });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (isDuplicate, existing) = await _duplicateDetector.CheckForDuplicateAsync(userId, categoryId, title);

        if (isDuplicate && existing != null)
        {
            return Ok(new
            {
                isDuplicate = true,
                existingReference = existing.ReferenceNumber,
                existingTitle = existing.Title,
                createdDate = existing.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
            });
        }

        return Ok(new { isDuplicate = false });
    }

    [HttpGet("metrics")]
    [Authorize]
    public async Task<IActionResult> GetMetrics()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.IsInRole(AppRoles.Administrator) ? AppRoles.Administrator
            : User.IsInRole(AppRoles.TeamLead) ? AppRoles.TeamLead
            : User.IsInRole(AppRoles.SupportAgent) ? AppRoles.SupportAgent
            : AppRoles.Requester;

        var metrics = await _requestService.GetDashboardMetricsAsync(userId, userRole);
        return Ok(metrics);
    }

    [HttpGet("simulate-error")]
    public IActionResult SimulateError()
    {
        _logger.LogInformation("Deliberately throwing simulated error to demonstrate correlation identifier tracking.");
        throw new InvalidOperationException("This is a controlled diagnostic error demonstrating structured correlation ID logging (AC-11).");
    }
}
