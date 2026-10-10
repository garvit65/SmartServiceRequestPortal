using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SendInAppNotificationAsync(
        string recipientId,
        string type,
        string message,
        int? relatedRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Message = message,
            RelatedRequestId = relatedRequestId,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SendEmailNotificationAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        // Mock email sender: logs the email safely without breaking transactional flows
        _logger.LogInformation("[EMAIL SENT] To: {Email} ({Name}) | Subject: {Subject} | Body preview: {Preview}",
            recipientEmail, recipientName, subject, body.Length > 80 ? body[..80] + "..." : body);

        return Task.CompletedTask;
    }

    public async Task NotifyRequestAssignedAsync(ServiceRequest request, ApplicationUser assignee, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendInAppNotificationAsync(
                assignee.Id,
                "RequestAssigned",
                $"You have been assigned to service request {request.ReferenceNumber}: '{request.Title}'.",
                request.Id,
                cancellationToken);

            if (!string.IsNullOrEmpty(assignee.Email))
            {
                await SendEmailNotificationAsync(
                    assignee.Email,
                    assignee.FullName,
                    $"Service Request Assigned: {request.ReferenceNumber}",
                    $"Hello {assignee.FullName},\n\nYou have been assigned to request {request.ReferenceNumber} ({request.Title}).\nPriority: {request.Priority}",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send assignment notification for request {Reference}", request.ReferenceNumber);
        }
    }

    public async Task NotifyCommentAddedAsync(ServiceRequest request, RequestComment comment, CancellationToken cancellationToken = default)
    {
        try
        {
            // If author is requester, notify assignee
            if (comment.AuthorId == request.RequesterId && !string.IsNullOrEmpty(request.AssigneeId))
            {
                await SendInAppNotificationAsync(
                    request.AssigneeId,
                    "NewComment",
                    $"Requester commented on {request.ReferenceNumber}: '{comment.Body[..Math.Min(50, comment.Body.Length)]}...'",
                    request.Id,
                    cancellationToken);
            }
            // If author is support and comment is public, notify requester
            else if (comment.Visibility == CommentVisibility.Public && comment.AuthorId != request.RequesterId)
            {
                await SendInAppNotificationAsync(
                    request.RequesterId,
                    "NewComment",
                    $"New update on your request {request.ReferenceNumber}: '{comment.Body[..Math.Min(50, comment.Body.Length)]}...'",
                    request.Id,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send comment notification for request {Reference}", request.ReferenceNumber);
        }
    }

    public async Task NotifyStatusChangedAsync(ServiceRequest request, string changedByName, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendInAppNotificationAsync(
                request.RequesterId,
                "StatusChanged",
                $"Your service request {request.ReferenceNumber} has moved to '{request.Status}' by {changedByName}.",
                request.Id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send status notification for request {Reference}", request.ReferenceNumber);
        }
    }

    public async Task NotifySlaBreachedAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(request.AssigneeId))
            {
                await SendInAppNotificationAsync(
                    request.AssigneeId,
                    "SlaBreached",
                    $"WARNING: Service request {request.ReferenceNumber} has breached its SLA deadline!",
                    request.Id,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SLA breach notification for request {Reference}", request.ReferenceNumber);
        }
    }
}
