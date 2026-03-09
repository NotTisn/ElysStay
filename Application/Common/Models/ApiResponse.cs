namespace Application.Common.Models;

/// <summary>
/// Standard API response envelope for single-item responses.
/// Matches spec §7 response format.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Fail(string message, string? errorCode = null, Dictionary<string, string[]>? errors = null) => new()
    {
        Success = false,
        Message = message,
        ErrorCode = errorCode,
        Errors = errors
    };
}

/// <summary>
/// Non-generic variant for error-only responses.
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message
    };

    public static ApiResponse Fail(string message, string? errorCode = null, Dictionary<string, string[]>? errors = null) => new()
    {
        Success = false,
        Message = message,
        ErrorCode = errorCode,
        Errors = errors
    };
}
