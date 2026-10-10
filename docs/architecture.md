# Architecture & Design Documentation
**Smart Service Request Management Portal (SSRMP)**  
*Contoso Support Services*

---

## 1. Architectural Overview & Clean Layering

The application is structured according to the Clean Architecture (Layered Architecture) pattern to ensure high maintainability, testability, and strict separation of concerns.

```
                           +------------------------+
                           |   ServicePortal.Web    |
                           |   (Presentation Layer) |
                           +-----------+------------+
                                       |
                   +-------------------+-------------------+
                   |                                       |
                   v                                       v
+-----------------------------+         +-------------------------------+
| ServicePortal.Infrastructure| ------> |   ServicePortal.Application   |
| (Persistence, Email, Jobs)  |         |   (Business Rules, DTOs, Intf)|
+-----------------------------+         +---------------+---------------+
                   |                                    |
                   +-----------------+------------------+
                                     |
                                     v
                        +------------------------+
                        |  ServicePortal.Domain  |
                        |  (Entities & Enums)    |
                        +------------------------+
```

### 1.1 Layer Responsibilities

1. **`ServicePortal.Domain`**:
   - Contains core domain entities (`ServiceRequest`, `ApplicationUser`, `SupportTeam`, `Category`, `RequestComment`, `RequestAttachment`, `RequestStatusHistory`, `AssignmentHistory`, `SlaPolicy`, `Notification`, `AuditLog`).
   - Contains domain enumerations (`RequestStatus`, `RequestPriority`, `CommentVisibility`, `ContactMethod`, `SlaStatus`, `AppRoles`).
   - Pure domain logic with zero external infrastructure dependencies.

2. **`ServicePortal.Application`**:
   - Contains interfaces (`IApplicationDbContext`, `ISlaCalculationService`, `IWorkflowService`, `IReferenceNumberGenerator`, `INotificationService`, `IAuditLogService`, `IFileStorageService`, `ICurrentUserService`).
   - Contains Data Transfer Objects (DTOs) and ViewModels (`CreateRequestDto`, `RequestDetailsDto`, `RequestFilterDto`, `PaginatedList<T>`, `DashboardMetricsDto`).
   - Houses core business rule validators (`SlaCalculationService`, `WorkflowService`, `ReferenceNumberGenerator`, `DuplicateRequestDetector`, `ServiceRequestService`).
   - Defines custom domain exceptions (`DomainValidationException`, `WorkflowTransitionException`, `NotFoundException`, `ConcurrencyConflictException`).

3. **`ServicePortal.Infrastructure`**:
   - Implements `IApplicationDbContext` via `ApplicationDbContext` with EF Core (SQL Server, SQLite, InMemory).
   - Handles ASP.NET Core Identity integration with password security, account lockouts, and role associations.
   - Houses background hosted services:
     - `SlaMonitoringBackgroundService`: Runs background evaluation of open requests against business-hour SLA deadlines and flags breaches.
     - `RequestAutoClosureBackgroundService`: Scans for resolved tickets older than 7 calendar days and closes them automatically.
   - Provides safe file storage outside the public web root (`FileStorageService`) with extension whitelisting and file size validation.
   - Includes data seeding via `DbInitializer.SeedAsync()`.

4. **`ServicePortal.Web`**:
   - Presentation layer built on ASP.NET Core MVC with Razor views and responsive Bootstrap 5 styling.
   - Implements custom middlewares:
     - `CorrelationIdMiddleware`: Injects structured correlation IDs across every request lifecycle.
     - `GlobalExceptionMiddleware`: Centralized exception trapping with user-friendly error views and correlation logging.
   - Provides RESTful Web API endpoints in `ApiController` for decoupled JSON clients.

5. **`tests/ServicePortal.UnitTests` & `tests/ServicePortal.IntegrationTests`**:
   - 20 Unit tests verifying business-hour calculations, weekend rollovers, workflow state machines, duplicate detection, and reference formatting.
   - 5 Integration tests verifying end-to-end authorization (AC-02), internal note redaction (AC-03), invalid transition rejection (AC-04), optimistic concurrency conflicts (AC-10), and automated background closure (AC-07).

---

## 2. Authentication & Fine-Grained Authorization

1. **Authentication**:
   - Implemented using ASP.NET Core Identity with secure password hashing (`PBKDF2`/`Argon2`).
   - Account lockout is automatically triggered after 5 consecutive failed login attempts (15-minute cooldown).
   - Inactive accounts (`IsActive == false`) are strictly denied authentication even when correct credentials are provided.
   - Cookie expiration with sliding window and anti-forgery token validation across all state-mutating requests.

2. **Role-Based Authorization**:
   - Four distinct user roles:
     - `Requester`: Scoped strictly to own submitted requests; cannot view internal comments; cannot assign tickets.
     - `SupportAgent`: Can view and work on tickets in their assigned team and tickets assigned to them; can add internal notes.
     - `TeamLead`: Can monitor team SLA compliance and assign/reassign tickets only to active agents within their team.
     - `Administrator`: Full system access, account activation/deactivation, category creation, and SLA target configuration.
   - Object Ownership Enforcement: Service layer validates that a Requester requesting ticket details or attachments matches the `RequesterId`, preventing Insecure Direct Object Reference (IDOR) vulnerabilities.

---

## 3. SLA Calculation Engine (Business Hours & Weekend Handling)

Contoso Support Services enforces strict operational business hours:
- **Active Hours**: Monday through Friday, 09:00 to 18:00 (9 hours per business day).
- **Excluded**: Saturdays, Sundays, and non-working hours.
- When a ticket is submitted at 16:00 on Friday with a 4-hour SLA:
  - 2 hours are consumed on Friday (16:00 to 18:00).
  - Saturday and Sunday are skipped.
  - The remaining 2 hours are allocated on Monday morning (09:00 to 11:00), setting the deadline to **Monday 11:00 UTC**.
- When a ticket is created during a weekend or off-hours, the clock start is automatically normalized to 09:00 UTC on the next business day.

---

## 4. Background Processing Architecture

The application registers two `IHostedService` / `BackgroundService` singletons:
1. **`SlaMonitoringBackgroundService`**:
   - Periodically wakes up every 60 seconds.
   - Uses an `IServiceScope` to resolve scoped database and calculation services.
   - Detects tickets where `now > ResponseDueAtUtc` (if unresponded) or `now > ResolutionDueAtUtc` (if unresolved).
   - Marks `IsSlaBreached = true`, logs an audit trail entry, and dispatches in-app notifications.
2. **`RequestAutoClosureBackgroundService`**:
   - Periodically scans for tickets in `Resolved` status where `ResolvedAtUtc <= DateTime.UtcNow.AddDays(-7)`.
   - Automatically transitions status to `Closed`, setting `ClosedAtUtc`.
   - Records an automated `RequestStatusHistory` entry indicating auto-closure without customer dispute.
