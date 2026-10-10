using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Ensure Database created
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Seed Support Teams
        if (!await context.SupportTeams.AnyAsync())
        {
            var teams = new List<SupportTeam>
            {
                new() { Name = "IT Support", Description = "Hardware, software, workstation, and network support.", IsActive = true },
                new() { Name = "HR Services", Description = "Human resources, benefits, payroll, and onboarding inquiries.", IsActive = true },
                new() { Name = "Facilities Management", Description = "Building access, office workspace, ergonomics, and maintenance.", IsActive = true }
            };
            await context.SupportTeams.AddRangeAsync(teams);
            await context.SaveChangesAsync();
        }

        var itTeam = await context.SupportTeams.FirstAsync(t => t.Name == "IT Support");
        var hrTeam = await context.SupportTeams.FirstAsync(t => t.Name == "HR Services");
        var facilitiesTeam = await context.SupportTeams.FirstAsync(t => t.Name == "Facilities Management");

        // 3. Seed Categories (mapped to Support Teams)
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "Hardware Issue", Description = "Laptops, monitors, peripherals, printers", SupportTeamId = itTeam.Id, IsActive = true },
                new() { Name = "Software Access & Licenses", Description = "Permissions, IDE licenses, cloud accounts", SupportTeamId = itTeam.Id, IsActive = true },
                new() { Name = "VPN & Network Connectivity", Description = "Office WiFi, remote VPN, firewalls", SupportTeamId = itTeam.Id, IsActive = true },
                new() { Name = "Payroll & Compensation", Description = "Salary questions, tax deductions, bonuses", SupportTeamId = hrTeam.Id, IsActive = true },
                new() { Name = "Benefits & Leave Balance", Description = "Medical insurance, annual leave, parental leave", SupportTeamId = hrTeam.Id, IsActive = true },
                new() { Name = "Desk Relocation & Setup", Description = "Seating arrangements, chair requests", SupportTeamId = facilitiesTeam.Id, IsActive = true },
                new() { Name = "Badge & Security Pass", Description = "Office entry card, visitor approvals", SupportTeamId = facilitiesTeam.Id, IsActive = true }
            };
            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        // 4. Seed SLA Policies
        if (!await context.SlaPolicies.AnyAsync())
        {
            var policies = new List<SlaPolicy>
            {
                new() { Priority = RequestPriority.Critical, ResponseTargetHours = 2, ResolutionTargetHours = 8, IsActive = true },
                new() { Priority = RequestPriority.High, ResponseTargetHours = 4, ResolutionTargetHours = 18, IsActive = true },
                new() { Priority = RequestPriority.Medium, ResponseTargetHours = 8, ResolutionTargetHours = 36, IsActive = true },
                new() { Priority = RequestPriority.Low, ResponseTargetHours = 16, ResolutionTargetHours = 72, IsActive = true }
            };
            await context.SlaPolicies.AddRangeAsync(policies);
            await context.SaveChangesAsync();
        }

        // 5. Seed Users
        async Task<ApplicationUser> EnsureUserAsync(string email, string fullName, string role, int? teamId, bool isActive = true)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true,
                    IsActive = isActive,
                    SupportTeamId = teamId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var createRes = await userManager.CreateAsync(user, "Password123!");
                if (createRes.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
            return user;
        }

        var adminUser = await EnsureUserAsync("admin@contoso.com", "Alice Administrator", AppRoles.Administrator, null);
        var itLead = await EnsureUserAsync("lead.it@contoso.com", "Liam IT-Lead", AppRoles.TeamLead, itTeam.Id);
        var hrLead = await EnsureUserAsync("lead.hr@contoso.com", "Hannah HR-Lead", AppRoles.TeamLead, hrTeam.Id);
        var itAgent1 = await EnsureUserAsync("agent.it1@contoso.com", "Ian IT-Agent", AppRoles.SupportAgent, itTeam.Id);
        var itAgent2 = await EnsureUserAsync("agent.it2@contoso.com", "Iris IT-Agent", AppRoles.SupportAgent, itTeam.Id);
        var hrAgent = await EnsureUserAsync("agent.hr@contoso.com", "Harry HR-Agent", AppRoles.SupportAgent, hrTeam.Id);
        var requester1 = await EnsureUserAsync("john.requester@contoso.com", "John Employee", AppRoles.Requester, null);
        var requester2 = await EnsureUserAsync("sara.requester@contoso.com", "Sara Customer", AppRoles.Requester, null);
        await EnsureUserAsync("inactive.user@contoso.com", "Inactive Staff", AppRoles.Requester, null, isActive: false);

        // 6. Seed Demo Service Requests
        if (!await context.ServiceRequests.AnyAsync())
        {
            var hwCat = await context.Categories.FirstAsync(c => c.Name == "Hardware Issue");
            var softCat = await context.Categories.FirstAsync(c => c.Name == "Software Access & Licenses");
            var payCat = await context.Categories.FirstAsync(c => c.Name == "Payroll & Compensation");
            var deskCat = await context.Categories.FirstAsync(c => c.Name == "Desk Relocation & Setup");

            var now = DateTime.UtcNow;

            var r1 = new ServiceRequest
            {
                ReferenceNumber = "SR-2026-000001",
                Title = "Laptop battery drains in 30 minutes",
                Description = "My Dell laptop shuts down quickly without AC adapter connected. Need replacement battery or unit inspection.",
                CategoryId = hwCat.Id,
                AssignedTeamId = itTeam.Id,
                AssigneeId = itAgent1.Id,
                RequesterId = requester1.Id,
                Priority = RequestPriority.High,
                Status = RequestStatus.InProgress,
                PreferredContactMethod = ContactMethod.Email,
                CreatedAtUtc = now.AddDays(-2),
                ResponseDueAtUtc = now.AddDays(-2).AddHours(4),
                ResolutionDueAtUtc = now.AddDays(1),
                FirstRespondedAtUtc = now.AddDays(-2).AddHours(1),
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            var r2 = new ServiceRequest
            {
                ReferenceNumber = "SR-2026-000002",
                Title = "Visual Studio Enterprise license key requested",
                Description = "Please assign a license subscription for VS 2026 Enterprise for new project onboarding.",
                CategoryId = softCat.Id,
                AssignedTeamId = itTeam.Id,
                AssigneeId = null,
                RequesterId = requester1.Id,
                Priority = RequestPriority.Medium,
                Status = RequestStatus.New,
                PreferredContactMethod = ContactMethod.Portal,
                CreatedAtUtc = now.AddHours(-3),
                ResponseDueAtUtc = now.AddHours(5),
                ResolutionDueAtUtc = now.AddDays(3),
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            var r3 = new ServiceRequest
            {
                ReferenceNumber = "SR-2026-000003",
                Title = "Clarification on tax deduction in September payslip",
                Description = "There was an unexpected additional tax deduction. Can HR check the investment proof submission?",
                CategoryId = payCat.Id,
                AssignedTeamId = hrTeam.Id,
                AssigneeId = hrAgent.Id,
                RequesterId = requester2.Id,
                Priority = RequestPriority.Medium,
                Status = RequestStatus.WaitingForRequester,
                PreferredContactMethod = ContactMethod.Email,
                CreatedAtUtc = now.AddDays(-3),
                ResponseDueAtUtc = now.AddDays(-3).AddHours(8),
                ResolutionDueAtUtc = now.AddDays(1),
                FirstRespondedAtUtc = now.AddDays(-3).AddHours(2),
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            var r4 = new ServiceRequest
            {
                ReferenceNumber = "SR-2026-000004",
                Title = "Standing desk converter installation",
                Description = "Doctor recommended standing desk setup for lower back ergonomics.",
                CategoryId = deskCat.Id,
                AssignedTeamId = facilitiesTeam.Id,
                AssigneeId = null,
                RequesterId = requester2.Id,
                Priority = RequestPriority.Low,
                Status = RequestStatus.Resolved,
                PreferredContactMethod = ContactMethod.Portal,
                CreatedAtUtc = now.AddDays(-5),
                ResponseDueAtUtc = now.AddDays(-4),
                ResolutionDueAtUtc = now.AddDays(-1),
                FirstRespondedAtUtc = now.AddDays(-4),
                ResolvedAtUtc = now.AddDays(-2),
                ResolutionSummary = "Standing desk converter installed and tested at desk #304.",
                ResolutionCategory = "Completed Successfully",
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            var r5 = new ServiceRequest
            {
                ReferenceNumber = "SR-2026-000005",
                Title = "Production VPN Gateway connectivity failure",
                Description = "Engineers cannot reach cloud staging and production clusters via Contoso-VPN.",
                CategoryId = hwCat.Id,
                AssignedTeamId = itTeam.Id,
                AssigneeId = itAgent2.Id,
                RequesterId = requester1.Id,
                Priority = RequestPriority.Critical,
                Status = RequestStatus.InProgress,
                PreferredContactMethod = ContactMethod.Phone,
                CreatedAtUtc = now.AddHours(-10),
                ResponseDueAtUtc = now.AddHours(-8), // Breached
                ResolutionDueAtUtc = now.AddHours(-2), // Breached
                FirstRespondedAtUtc = now.AddHours(-7),
                IsSlaBreached = true,
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            await context.ServiceRequests.AddRangeAsync(r1, r2, r3, r4, r5);
            await context.SaveChangesAsync();

            // Comments
            context.RequestComments.AddRange(
                new RequestComment
                {
                    ServiceRequestId = r1.Id,
                    AuthorId = requester1.Id,
                    Visibility = CommentVisibility.Public,
                    Body = "Attaching battery health report diagnostics.",
                    CreatedAtUtc = now.AddDays(-2).AddHours(1)
                },
                new RequestComment
                {
                    ServiceRequestId = r1.Id,
                    AuthorId = itAgent1.Id,
                    Visibility = CommentVisibility.Internal,
                    Body = "Ordered spare OEM battery from vendor under warranty coverage.",
                    CreatedAtUtc = now.AddDays(-1)
                },
                new RequestComment
                {
                    ServiceRequestId = r3.Id,
                    AuthorId = hrAgent.Id,
                    Visibility = CommentVisibility.Public,
                    Body = "Please upload the revised Form 12BB receipt so we can adjust it in the upcoming payroll cycle.",
                    CreatedAtUtc = now.AddDays(-2)
                }
            );

            // Status histories
            context.RequestStatusHistories.AddRange(
                new RequestStatusHistory { ServiceRequestId = r1.Id, FromStatus = RequestStatus.New, ToStatus = RequestStatus.Assigned, ChangedById = itLead.Id, Reason = "Assigned to primary hardware tech.", ChangedAtUtc = now.AddDays(-2) },
                new RequestStatusHistory { ServiceRequestId = r1.Id, FromStatus = RequestStatus.Assigned, ToStatus = RequestStatus.InProgress, ChangedById = itAgent1.Id, Reason = "Hardware diagnosis started.", ChangedAtUtc = now.AddDays(-2).AddHours(1) },
                new RequestStatusHistory { ServiceRequestId = r3.Id, FromStatus = RequestStatus.InProgress, ToStatus = RequestStatus.WaitingForRequester, ChangedById = hrAgent.Id, Reason = "Awaiting Form 12BB tax proof document.", ChangedAtUtc = now.AddDays(-2) },
                new RequestStatusHistory { ServiceRequestId = r4.Id, FromStatus = RequestStatus.InProgress, ToStatus = RequestStatus.Resolved, ChangedById = adminUser.Id, Reason = "Setup completed and verified.", ChangedAtUtc = now.AddDays(-2) }
            );

            await context.SaveChangesAsync();
        }
    }
}
