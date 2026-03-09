namespace Application.Common.Exceptions;

/// <summary>
/// Thrown for invalid input that doesn't fit FluentValidation patterns. Maps to HTTP 400.
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}
