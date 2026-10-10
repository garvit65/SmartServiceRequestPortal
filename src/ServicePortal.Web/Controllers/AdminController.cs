using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Web.Controllers;

[Authorize(Roles = AppRoles.Administrator)]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        IAuditLogService auditLogService)
    {
        _userManager = userManager;
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users
            .Include(u => u.SupportTeam)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var userListWithRoles = new List<(ApplicationUser User, IList<string> Roles)>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            userListWithRoles.Add((u, roles));
        }

        ViewBag.Teams = await _context.SupportTeams.ToListAsync();
        return View(userListWithRoles);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync(
            User.Identity?.Name,
            "Administrator",
            "ToggleUserStatus",
            nameof(ApplicationUser),
            user.Id,
            $"User {user.Email} status set to {(user.IsActive ? "Active" : "Inactive")}.");

        TempData["SuccessMessage"] = $"User {user.Email} is now {(user.IsActive ? "Active" : "Deactivated")}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(string userId, string role, int? teamId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);

        user.SupportTeamId = teamId;
        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync(
            User.Identity?.Name,
            "Administrator",
            "UpdateUserRole",
            nameof(ApplicationUser),
            user.Id,
            $"Updated role of {user.Email} to {role} and assigned team ID: {teamId}.");

        TempData["SuccessMessage"] = $"Role for {user.Email} updated to {role}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var categories = await _context.Categories
            .Include(c => c.SupportTeam)
            .ToListAsync();

        ViewBag.Teams = new SelectList(await _context.SupportTeams.ToListAsync(), "Id", "Name");
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(string name, string description, int supportTeamId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Category name is required.";
            return RedirectToAction(nameof(Categories));
        }

        var cat = new Category
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            SupportTeamId = supportTeamId,
            IsActive = true
        };

        _context.Categories.Add(cat);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Category '{name}' added successfully.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<IActionResult> SlaPolicies()
    {
        var policies = await _context.SlaPolicies
            .OrderBy(p => p.Priority)
            .ToListAsync();

        return View(policies);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSlaPolicy(int id, int responseHours, int resolutionHours)
    {
        var policy = await _context.SlaPolicies.FindAsync(id);
        if (policy == null) return NotFound();

        policy.ResponseTargetHours = responseHours;
        policy.ResolutionTargetHours = resolutionHours;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"SLA policy for {policy.Priority} updated successfully.";
        return RedirectToAction(nameof(SlaPolicies));
    }

    [HttpGet]
    public async Task<IActionResult> AuditLogs(string? correlationId)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(a => a.CorrelationId == correlationId.Trim());
        }

        var logs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(100)
            .ToListAsync();

        ViewBag.CorrelationId = correlationId;
        return View(logs);
    }
}
