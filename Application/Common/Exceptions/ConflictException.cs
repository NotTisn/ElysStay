namespace Application.Common.Exceptions;

/// <summary>
/// Thrown on business rule violations or state conflicts. Maps to HTTP 409.
/// </summary>
public class ConflictException : Exception
{
    public string ErrorCode { get; }

    public ConflictException(string message, string errorCode = "CONFLICT")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
