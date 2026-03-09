using Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Base controller with standard route prefix and common helpers.
/// All API controllers inherit from this.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Returns 200 with a typed data envelope.
    /// </summary>
    protected IActionResult OkResponse<T>(T data, string? message = null)
        => Ok(ApiResponse<T>.Ok(data, message));

    /// <summary>
    /// Returns 201 with a typed data envelope and optional location header.
    /// </summary>
    protected IActionResult CreatedResponse<T>(T data, string? routeName = null, object? routeValues = null, string? message = null)
    {
        if (routeName is not null)
            return CreatedAtRoute(routeName, routeValues, ApiResponse<T>.Ok(data, message));

        return StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Ok(data, message));
    }

    /// <summary>
    /// Returns 204 No Content.
    /// </summary>
    protected new IActionResult NoContent() => base.NoContent();

    /// <summary>
    /// Returns a paginated response.
    /// </summary>
    protected IActionResult PagedOk<T>(PagedResult<T> result)
        => Ok(result.ToPagedResponse());
}
