# API & Endpoint Summary
**Smart Service Request Management Portal (SSRMP)**

The portal exposes both server-rendered MVC endpoints and decoupled RESTful JSON endpoints.

---

## 1. REST API Endpoints (`/api/...`)

| Method | Route | Authorization | Description |
|---|---|---|---|
| `GET` | `/api/requests/{id}` | Required (`Requester`, `Agent`, `Lead`, `Admin`) | Returns full JSON details of a request. **Enforces AC-03**: internal comments are redacted for Requesters. |
| `GET` | `/api/check-duplicate` | Required | Checks whether an active ticket with a matching title exists in the selected category created within 24 hours. |
| `GET` | `/api/metrics` | Required | Aggregates role-specific metrics, counts, SLA compliance percentage, and workload distribution. |
| `GET` | `/api/simulate-error` | Anonymous / Public | Intentionally throws a handled exception to demonstrate structured logging and the `X-Correlation-ID` header (AC-11). |

---

## 2. Core MVC Controller Endpoints

### 2.1 Service Requests (`/Requests/...`)

| Method | Route | Description |
|---|---|---|
| `GET` | `/Requests/Index` | Search, filter by category/team/priority/SLA, sort, and server-side paginated request catalog. |
| `GET` | `/Requests/Details/{id}` | Full request view, tabs for comments/timeline, action modals for state transitions. |
| `GET` | `/Requests/Create` | Displays new request form with category routing dropdown. |
| `POST` | `/Requests/Create` | Submits new request; handles attachment uploads; validates duplicate warning acknowledgment. |
| `POST` | `/Requests/Assign` | Team Lead/Admin assigns request to an active support agent in that team. Validates concurrency token. |
| `POST` | `/Requests/TransitionStatus` | Validates and triggers status transitions (InProgress, WaitingForRequester, Resolved, Closed, Reopened). |
| `POST` | `/Requests/AddComment` | Posts public comment or internal note. Requesters are restricted to public comments only. |
| `GET` | `/Requests/DownloadAttachment/{id}` | Securely streams attachment from disk storage outside web root. Validates user ownership. |
| `GET` | `/Requests/ExportCsv` | Generates UTF-8 CSV containing currently authorized and filtered rows. Hides internal comments. |

### 2.2 Account & Authentication (`/Account/...`)

| Method | Route | Description |
|---|---|---|
| `GET` | `/Account/Login` | Login screen with email and password fields. |
| `POST` | `/Account/Login` | Authenticates user; validates `IsActive` (denies inactive users); tracks 5-attempt lockout. |
| `GET` | `/Account/Register` | Registers new Requester account with verified email status. |
| `POST` | `/Account/Register` | Validates model and creates user under `Requester` role. |
| `POST` | `/Account/Logout` | Signs out user and clears authentication cookie. |
| `GET` | `/Account/QuickLogin?role={role}` | Evaluator shortcut to instantly authenticate demo personas (`requester`, `agent`, `lead`, `admin`, `inactive`). |

### 2.3 Administration (`/Admin/...`)

| Method | Route | Description |
|---|---|---|
| `GET` | `/Admin/Users` | Lists all users, active status, roles, and assigned teams. |
| `POST` | `/Admin/ToggleUserStatus` | Activates or deactivates an account. |
| `POST` | `/Admin/UpdateUserRole` | Updates user role and assigns support team membership. |
| `GET` | `/Admin/Categories` | Lists categories and team routing mappings. |
| `POST` | `/Admin/AddCategory` | Creates new service request category mapped to a support team. |
| `GET` | `/Admin/SlaPolicies` | Displays SLA policies per priority (Critical, High, Medium, Low). |
| `POST` | `/Admin/UpdateSlaPolicy` | Updates target business hours for response and resolution. |
| `GET` | `/Admin/AuditLogs` | Chronological audit trail viewer with correlation ID filtering. |
