using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IServiceRequestService _requestService;

    public DashboardController(IServiceRequestService requestService)
    {
        _requestService = requestService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.IsInRole(AppRoles.Administrator) ? AppRoles.Administrator
            : User.IsInRole(AppRoles.TeamLead) ? AppRoles.TeamLead
            : User.IsInRole(AppRoles.SupportAgent) ? AppRoles.SupportAgent
            : AppRoles.Requester;

        var metrics = await _requestService.GetDashboardMetricsAsync(userId, userRole);
        return View(metrics);
    }
}
