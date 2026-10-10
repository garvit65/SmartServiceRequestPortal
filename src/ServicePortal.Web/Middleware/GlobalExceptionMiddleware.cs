using System.Net;
using System.Text.Json;
using ServicePortal.Application.Exceptions;

namespace ServicePortal.Web.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N");
            _logger.LogError(ex, "Unhandled exception occurred. CorrelationId: {CorrelationId}, Message: {Message}", correlationId, ex.Message);

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                await HandleApiExceptionAsync(context, ex, correlationId);
            }
            else
            {
                // For web page requests, redirect or forward to user-friendly error view
                context.Response.Redirect($"/Home/Error?correlationId={WebUtility.UrlEncode(correlationId)}");
            }
        }
    }

    private static async Task HandleApiExceptionAsync(HttpContext context, Exception ex, string correlationId)
    {
        context.Response.ContentType = "application/json";

        var statusCode = ex switch
        {
            DomainValidationException => HttpStatusCode.BadRequest,
            WorkflowTransitionException => HttpStatusCode.BadRequest,
            NotFoundException => HttpStatusCode.NotFound,
            ConcurrencyConflictException => HttpStatusCode.Conflict,
            UnauthorizedAccessException => HttpStatusCode.Forbidden,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = (int)statusCode,
            message = statusCode == HttpStatusCode.InternalServerError ? "An unexpected error occurred. Please contact support with the correlation ID." : ex.Message,
            correlationId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
