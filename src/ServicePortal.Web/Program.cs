using Microsoft.AspNetCore.Identity;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure;
using ServicePortal.Infrastructure.Data;
using ServicePortal.Web.Middleware;
using ServicePortal.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Infrastructure Services (EF Core, Identity, Repositories, Background Jobs)
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Add Web Application Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequesterOnly", policy => policy.RequireRole(AppRoles.Requester, AppRoles.Administrator));
    options.AddPolicy("SupportAgentOnly", policy => policy.RequireRole(AppRoles.SupportAgent, AppRoles.Administrator));
    options.AddPolicy("TeamLeadOnly", policy => policy.RequireRole(AppRoles.TeamLead, AppRoles.Administrator));
    options.AddPolicy("AdministratorOnly", policy => policy.RequireRole(AppRoles.Administrator));
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// 3. Database Initialization & Seeding at Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbInitializer.SeedAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// 4. Custom Middlewares (Correlation ID and Centralized Exception Handling)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Required for WebApplicationFactory in IntegrationTests
public partial class Program { }
