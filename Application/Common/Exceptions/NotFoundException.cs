namespace Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested entity is not found. Maps to HTTP 404.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}
