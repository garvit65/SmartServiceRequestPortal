namespace ServicePortal.Domain.Enums;

public enum RequestStatus
{
    New = 1,
    Assigned = 2,
    InProgress = 3,
    WaitingForRequester = 4,
    Resolved = 5,
    Closed = 6
}

public enum RequestPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum CommentVisibility
{
    Public = 1,
    Internal = 2
}

public enum ContactMethod
{
    Email = 1,
    Phone = 2,
    Portal = 3
}

public enum SlaStatus
{
    WithinSla = 1,
    ApproachingBreach = 2,
    Breached = 3
}

public static class AppRoles
{
    public const string Requester = "Requester";
    public const string SupportAgent = "SupportAgent";
    public const string TeamLead = "TeamLead";
    public const string Administrator = "Administrator";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Requester,
        SupportAgent,
        TeamLead,
        Administrator
    };
}
