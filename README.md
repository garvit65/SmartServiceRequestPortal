# Smart Service Request Management Portal (SSRMP)
**Contoso Support Services**

A production-style enterprise web portal built with **.NET 10 / C#**, **ASP.NET Core MVC**, **Entity Framework Core**, and **ASP.NET Core Identity**. The portal centralizes IT, facilities, and HR support operations through automated team routing, SLA monitoring, role-based workflows, immutable audit trails, and background processing.

---

## 1. Project Overview & Features

- **End-to-End Request Lifecycle**: `New -> Assigned -> In Progress -> Waiting for Requester -> Resolved -> Closed`.
- **Role-Based Workflows**: Tailored portals and dashboards for **Requester**, **Support Agent**, **Team Lead**, and **Administrator**.
- **Automated Routing**: Selecting a category automatically maps and routes the ticket to its corresponding support team.
- **Business Hours SLA Engine**: Calculates response and resolution due dates strictly within Monday–Friday 09:00–18:00 (excluding weekends).
- **Background Processing**:
  - `SlaMonitoringBackgroundService`: Detects approaching and breached SLAs every minute, dispatching alerts.
  - `RequestAutoClosureBackgroundService`: Automatically transitions resolved tickets older than 7 calendar days to closed.
- **Privacy & Security**: Internal notes are completely redacted for requesters across views, CSV exports, and REST API responses (AC-03).
- **Object Ownership & Anti-IDOR**: Requesters can only access their own tickets; attempts to access unauthorized records return access denied (AC-02).
- **Concurrency Protection**: Optimistic concurrency tokens (`RowVersion`) prevent race conditions during ticket assignments and status updates (AC-10).
- **Centralized Error Handling**: Custom middlewares assign structured `X-Correlation-ID` headers and prevent stack trace leaks (AC-11).

---

## 2. Project Structure

```
SmartServiceRequestPortal.slnx
│
├── src/
│   ├── ServicePortal.Domain/             # Entities, Enums, Role definitions
│   │   ├── Entities/                     # ServiceRequest, ApplicationUser, SupportTeam, etc.
│   │   └── Enums/                        # RequestStatus, RequestPriority, SlaStatus, etc.
│   │
│   ├── ServicePortal.Application/        # Interfaces, DTOs, Business services, Exceptions
│   │   ├── DTOs/                         # CreateRequestDto, RequestDetailsDto, PaginatedList, etc.
│   │   ├── Exceptions/                   # DomainValidationException, WorkflowTransitionException
│   │   ├── Interfaces/                   # IApplicationDbContext, ISlaCalculationService, etc.
│   │   └── Services/                     # SlaCalculationService, WorkflowService, ServiceRequestService
│   │
│   ├── ServicePortal.Infrastructure/     # Persistence, Migrations, Background Jobs, Storage
│   │   ├── BackgroundJobs/               # SlaMonitoringBackgroundService, RequestAutoClosureBackgroundService
│   │   ├── Data/                         # ApplicationDbContext, DbInitializer, Migrations
│   │   └── Services/                     # NotificationService, AuditLogService, FileStorageService
│   │
│   └── ServicePortal.Web/                # ASP.NET Core MVC Web App, Controllers, Views, REST API
│       ├── Controllers/                  # RequestsController, AccountController, AdminController, ApiController
│       ├── Middleware/                   # CorrelationIdMiddleware, GlobalExceptionMiddleware
│       └── Views/                        # Razor Views for Dashboard, Requests, Admin, Notifications
│
├── tests/
│   ├── ServicePortal.UnitTests/          # 20 Unit tests (SLA, workflow, duplicate detector, sequence generator)
│   └── ServicePortal.IntegrationTests/   # 5 Integration tests (AC-02, AC-03, AC-04, AC-10, AC-07)
│
└── docs/
    ├── architecture.md                   # Clean architecture notes & design tradeoffs
    ├── er-diagram.md                     # Mermaid Entity-Relationship diagram
    ├── api-summary.md                    # REST API & MVC endpoint catalog
    └── copilot-report.md                 # Copilot utilization and edge cases analysis
```

---

## 3. Prerequisites

- [.NET 8 or .NET 10 SDK](https://dotnet.microsoft.com/download) (installed on host)
- [SQL Server](https://www.microsoft.com/sql-server) (MSSQLLocalDB, SQLEXPRESS, or SQL Server 2019/2022) **OR** SQLite (built-in fallback)
- Git (for version control)

---

## 4. Setup, Configuration & Running

### 4.1 Clone & Restore
```powershell
git clone <repository-url>
cd C#.NetAssignment
dotnet restore SmartServiceRequestPortal.slnx
```

### 4.2 Database Configuration & Migrations
The application defaults to SQL Server LocalDB (`Server=(localdb)\\mssqllocaldb;Database=SmartServicePortalDb;...`), and automatically creates and seeds the schema on initial startup via `DbInitializer.SeedAsync()`.

If you prefer SQLite or SQL Express, adjust `appsettings.json`:
```json
{
  "DatabaseProvider": "SqlServer", // Options: "SqlServer" or "Sqlite"
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SmartServicePortalDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
    "SqliteConnection": "Data Source=smartportal.db"
  }
}
```

To apply EF Core migrations manually using the dotnet-ef tool:
```powershell
dotnet ef database update --project src/ServicePortal.Infrastructure/ServicePortal.Infrastructure.csproj --startup-project src/ServicePortal.Web/ServicePortal.Web.csproj
```

### 4.3 Run the Web Application
```powershell
dotnet run --project src/ServicePortal.Web/ServicePortal.Web.csproj
```
Once launched, open your browser at `https://localhost:5001` or `http://localhost:5000`.

---

## 5. Pre-Seeded Demo Accounts (Credentials)

All accounts share the default password: **`Password123!`**

| Persona | Role | Email | Scope & Responsibilities |
|---|---|---|---|
| **John Employee** | Requester | `john.requester@contoso.com` | Submit, track own tickets, add attachments, confirm resolution. |
| **Sara Customer** | Requester | `sara.requester@contoso.com` | Submit tickets, test duplicate warning detection. |
| **Ian IT-Agent** | Support Agent | `agent.it1@contoso.com` | Assigned IT tickets, add internal notes, move to in-progress, resolve. |
| **Liam IT-Lead** | Team Lead | `lead.it@contoso.com` | IT Support team lead; assign team tickets, monitor team SLA performance. |
| **Hannah HR-Lead**| Team Lead | `lead.hr@contoso.com` | HR Services team lead; assign HR tickets, monitor workload. |
| **Alice Admin** | Administrator | `admin@contoso.com` | Full administrative access, manage users, categories, SLA policies, view audit logs. |
| **Inactive Staff** | Requester | `inactive.user@contoso.com` | Deactivated user account (demonstrates login rejection for inactive accounts). |

> **Evaluation Shortcut**: Use the **"Quick Switch Role"** dropdown located on the top navigation bar to switch between personas instantly without manually entering credentials!

---

## 6. Running Automated Tests

Run the complete automated test suite (Unit Tests + Integration Tests) with a single command:

```powershell
dotnet test SmartServiceRequestPortal.slnx
```

### Test Suite Summary:
- **`ServicePortal.UnitTests`**: **20 passed** (SLA calculation with weekend exclusion, state machine transitions, duplicate detection, sequence generation).
- **`ServicePortal.IntegrationTests`**: **5 passed** (AC-02 IDOR authorization boundary, AC-03 internal comment redaction, AC-04 invalid transition rejection, AC-10 optimistic concurrency, AC-07 auto-closure background job).
- **Total Test Results**: **25 Passed (0 Failed, 0 Skipped)**.

---

## 7. Acceptance Criteria Verification

| ID | Criterion | Implementation Evidence |
|---|---|---|
| **AC-01** | Verified requester creates request & receives unique reference | `ReferenceNumberGenerator` generates `SR-2026-000001` format under concurrency lock. |
| **AC-02** | Unauthorized user cannot view/modify another user's request | `ServiceRequestService.GetRequestDetailsAsync` enforces ownership; covered by integration test. |
| **AC-03** | Internal comments never appear in requester views, exports, or API | Filter `!isRequester || c.Visibility == Public` enforced on service, CSV, and API responses. |
| **AC-04** | Invalid status transitions rejected on server | `WorkflowService` rejects illegal transitions and raises `WorkflowTransitionException`. |
| **AC-05** | Team lead can assign requests only to active agents in their team | `AssignRequestAsync` checks `assignee.SupportTeamId == lead.SupportTeamId` and `IsActive`. |
| **AC-06** | System calculates and displays SLA breach state per policy | `SlaCalculationService` calculates business hours (Mon-Fri 09-18) excluding weekends. |
| **AC-07** | Automatic closure process closes eligible requests after 7 days | `RequestAutoClosureBackgroundService` automatically closes resolved tickets >= 7 days old. |
| **AC-08** | Search, filter, sorting, and pagination work seamlessly | `FilterRequestsAsync` supports multi-parameter server-side pagination (`PaginatedList<T>`). |
| **AC-09** | CSV export contains only authorized and filtered rows | `ExportCsvAsync` streams CSV respecting role authorization and redacting internal notes. |
| **AC-10** | Concurrent updates do not overwrite data; conflict returned | Optimistic concurrency token `RowVersion` validated before update, returning friendly conflict message. |
| **AC-11** | Errors logged with correlation identifier without exposing details | `CorrelationIdMiddleware` and `GlobalExceptionMiddleware` log correlation ID and display friendly view. |
| **AC-12** | Automated tests cover business rules and authorization | 20 unit tests and 5 integration tests pass with 100% success rate. |

---

## 8. Learning Module Concepts Implemented

1. **OOP - Inheritance & Polymorphism**:
   - `ApplicationUser` inherits `IdentityUser`.
   - `SlaMonitoringBackgroundService` and `RequestAutoClosureBackgroundService` inherit `BackgroundService` and override `ExecuteAsync()`.
   - Polymorphic method overloading in `ISlaCalculationService`.
2. **Interfaces & Abstraction**:
   - Clean interfaces (`IApplicationDbContext`, `ISlaCalculationService`, `IWorkflowService`, `INotificationService`, `IAuditLogService`, `IFileStorageService`) decoupling domain logic from EF Core and I/O.
3. **Encapsulation & Data Validation**:
   - Domain entity properties encapsulated with data annotations, custom domain exceptions, and state machine guards.
4. **Collections - List & Dictionary**:
   - Entity navigation collections (`ICollection<T>`), lookup dictionaries (`Dictionary<string, int>`) for dashboard distributions and agent workload metrics.
5. **LINQ Basics & Querying**:
   - Advanced LINQ queries using `.Where()`, `.OrderByDescending()`, `.GroupBy()`, `.Select()`, `.Skip()`, `.Take()`, `.Any()`, `.Count()`.
6. **Async and Await Basics**:
   - Fully asynchronous non-blocking I/O throughout all database operations, file streaming, notifications, and background processes using `Task`, `await`, and `CancellationToken`.
7. **ASP.NET Core MVC & Routing**:
   - Model-View-Controller architecture with tag helpers, attribute and conventional routing, strongly-typed ViewModels, and Razor views.
8. **REST API / Web API Basics**:
   - `ApiController` exposing JSON endpoints for request details, dynamic duplicate checking, and diagnostic trace simulation.
9. **Dependency Injection**:
   - Scoped, Singleton, and Hosted services registered cleanly via extension methods in `DependencyInjection.cs`.
10. **Entity Framework Core Code-First Migrations**:
    - Fluent API configurations, indexes, composite relationships, and migrations (`InitialCreate`).

---

## 9. Known Limitations & Future Enhancements

- **Public Holiday Calendar**: SLA currently excludes weekends (Mon-Fri 09:00–18:00). A future release could integrate national and regional holiday schedules.
- **Real-Time WebSockets**: Notifications currently poll every 15 seconds; integrating SignalR would deliver real-time push notifications.
- **Multi-Tenancy**: The current design supports one enterprise organization; adding a `TenantId` discriminator would enable multi-organization SaaS deployments.
