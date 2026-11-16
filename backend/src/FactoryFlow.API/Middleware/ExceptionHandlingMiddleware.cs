using System.Net;
using System.Text.Json;
using FactoryFlow.Domain.Common;
using FluentValidation;

namespace FactoryFlow.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred.";
        object? errors = null;

        switch (exception)
        {
            case DomainException domainEx:
                statusCode = HttpStatusCode.BadRequest;
                message = domainEx.Message;
                break;
            case ValidationException valEx:
                statusCode = HttpStatusCode.BadRequest;
                message = "Validation failed.";
                errors = valEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
                break;
            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                message = "Unauthorized access.";
                break;
            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                message = exception.Message;
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = JsonSerializer.Serialize(new
        {
            status = (int)statusCode,
            message,
            errors
        });

        return context.Response.WriteAsync(payload);
    }
}

