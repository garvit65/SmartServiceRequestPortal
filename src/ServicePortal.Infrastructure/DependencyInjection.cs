using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServicePortal.Application.Interfaces;
using ServicePortal.Application.Services;
using ServicePortal.Domain.Entities;
using ServicePortal.Infrastructure.BackgroundJobs;
using ServicePortal.Infrastructure.Data;
using ServicePortal.Infrastructure.Services;

namespace ServicePortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbProvider = configuration["DatabaseProvider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=SmartServicePortalDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (dbProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                var inMemName = configuration["ConnectionStrings:InMemoryConnection"] ?? "TestDb";
                options.UseInMemoryDatabase(inMemName);
            }
            else if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqliteConn = configuration.GetConnectionString("SqliteConnection") ?? "Data Source=smartportal.db";
                options.UseSqlite(sqliteConn);
            }
            else
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Identity Configuration
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // Password policy
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Application Services Registration (Dependency Injection)
        services.AddScoped<ISlaCalculationService, SlaCalculationService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();
        services.AddScoped<DuplicateRequestDetector>();
        services.AddScoped<IServiceRequestService, ServiceRequestService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddSingleton<IFileStorageService, FileStorageService>();

        // Background Hosted Services
        services.AddHostedService<SlaMonitoringBackgroundService>();
        services.AddHostedService<RequestAutoClosureBackgroundService>();

        return services;
    }
}
