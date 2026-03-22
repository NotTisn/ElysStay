namespace Application.Common.Exceptions;

/// <summary>
/// Thrown when the authenticated user lacks permission. Maps to HTTP 403.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.")
        : base(message) { }
}
