using ServicePortal.Application.DTOs;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Interfaces;

public interface IServiceRequestService
{
    Task<(bool Success, string? DuplicateWarning, ServiceRequest? Request)> CreateRequestAsync(
        CreateRequestDto dto,
        string requesterId,
        List<(Stream Stream, string FileName, string ContentType)> attachments,
        CancellationToken cancellationToken = default);

    Task<RequestDetailsDto?> GetRequestDetailsAsync(
        int id,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<RequestListItemDto>> FilterRequestsAsync(
        RequestFilterDto filter,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);

    Task AssignRequestAsync(
        AssignRequestDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task AddCommentAsync(
        AddCommentDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task ResolveRequestAsync(
        ResolveRequestDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task ReopenRequestAsync(
        ReopenRequestDto dto,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task CloseRequestAsync(
        int requestId,
        string rowVersion,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportCsvAsync(
        RequestFilterDto filter,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);

    Task<DashboardMetricsDto> GetDashboardMetricsAsync(
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);
}
