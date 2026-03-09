namespace Application.Common.Exceptions;

/// <summary>
/// Thrown when the authenticated user lacks permission. Maps to HTTP 403.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message) { }
}
