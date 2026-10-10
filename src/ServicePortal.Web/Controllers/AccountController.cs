using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Web.Models;

namespace ServicePortal.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        // Enforce account active status (Requirement: Deny inactive users access even if their credentials are correct)
        if (!user.IsActive)
        {
            _logger.LogWarning("Deactivated user {Email} attempted login.", model.Email);
            ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an administrator.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _auditLogService.LogAsync(
                user.Id,
                user.FullName,
                "UserLogin",
                nameof(ApplicationUser),
                user.Id,
                "User logged in successfully.");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out for {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "Account is locked due to multiple failed login attempts. Please try again in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid login credentials.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim(),
            EmailConfirmed = true, // Verified for portal submission
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            // Default role is Requester
            await _userManager.AddToRoleAsync(user, AppRoles.Requester);

            await _auditLogService.LogAsync(
                user.Id,
                user.FullName,
                "UserRegistered",
                nameof(ApplicationUser),
                user.Id,
                "New user registered with Requester role.");

            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["SuccessMessage"] = "Your account has been registered and verified successfully! Welcome to the portal.";
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userName = User.Identity?.Name ?? "User";
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User {UserName} logged out.", userName);
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> QuickLogin(string role)
    {
        var email = role switch
        {
            "admin" => "admin@contoso.com",
            "lead" => "lead.it@contoso.com",
            "agent" => "agent.it1@contoso.com",
            "requester" => "john.requester@contoso.com",
            "inactive" => "inactive.user@contoso.com",
            _ => "john.requester@contoso.com"
        };

        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            if (!user.IsActive)
            {
                TempData["ErrorMessage"] = $"Demo account '{email}' is deactivated, verifying that inactive users are denied access.";
                return RedirectToAction("Login");
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["SuccessMessage"] = $"Signed in as {user.FullName} ({role.ToUpper()}).";
            return RedirectToAction("Index", "Dashboard");
        }

        TempData["ErrorMessage"] = $"Demo account for role {role} was not found. Please ensure seed data was initialized.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
