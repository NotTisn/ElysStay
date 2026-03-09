using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API.Middleware;

/// <summary>
/// Catches exceptions from the pipeline and converts them into consistent
/// ApiResponse error envelopes with the correct HTTP status code.
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                ApiResponse.Fail(
                    "One or more validation errors occurred.",
                    "VALIDATION_ERROR",
                    validationEx.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray()
                        )
                )
            ),

            BadRequestException badRequestEx => (
                StatusCodes.Status400BadRequest,
                ApiResponse.Fail(badRequestEx.Message, "BAD_REQUEST")
            ),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                ApiResponse.Fail(notFoundEx.Message, "NOT_FOUND")
            ),

            ForbiddenException forbiddenEx => (
                StatusCodes.Status403Forbidden,
                ApiResponse.Fail(forbiddenEx.Message, "FORBIDDEN")
            ),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                ApiResponse.Fail(conflictEx.Message, conflictEx.ErrorCode)
            ),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                ApiResponse.Fail(
                    "The record was modified by another request. Please retry.",
                    "CONCURRENCY_CONFLICT"
                )
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("An unexpected error occurred.")
            )
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception ({StatusCode}) on {Method} {Path}: {Message}",
                statusCode, context.Request.Method, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
