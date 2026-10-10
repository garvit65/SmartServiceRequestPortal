using ServicePortal.Domain.Entities;

namespace ServicePortal.Application.Interfaces;

public interface INotificationService
{
    Task SendInAppNotificationAsync(string recipientId, string type, string message, int? relatedRequestId = null, CancellationToken cancellationToken = default);
    Task SendEmailNotificationAsync(string recipientEmail, string recipientName, string subject, string body, CancellationToken cancellationToken = default);
    Task NotifyRequestAssignedAsync(ServiceRequest request, ApplicationUser assignee, CancellationToken cancellationToken = default);
    Task NotifyCommentAddedAsync(ServiceRequest request, RequestComment comment, CancellationToken cancellationToken = default);
    Task NotifyStatusChangedAsync(ServiceRequest request, string changedByName, CancellationToken cancellationToken = default);
    Task NotifySlaBreachedAsync(ServiceRequest request, CancellationToken cancellationToken = default);
}

public interface IAuditLogService
{
    Task LogAsync(string? actorId, string actorName, string action, string entityName, string entityId, string changeSummary, string? correlationId = null, CancellationToken cancellationToken = default);
}

public interface IFileStorageService
{
    Task<(string StoredFileName, long FileSizeBytes)> SaveFileAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default);
    Task<Stream> GetFileStreamAsync(string storedFileName, CancellationToken cancellationToken = default);
    bool DeleteFile(string storedFileName);
    bool IsAllowedExtension(string fileName);
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? UserEmail { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    string? CorrelationId { get; }
}
