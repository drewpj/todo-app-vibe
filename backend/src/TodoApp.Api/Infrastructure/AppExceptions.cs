namespace TodoApp.Api.Infrastructure;

/// <summary>Base type for expected failures. The global handler maps each one to an HTTP status.</summary>
public abstract class AppException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;
}

public sealed class NotFoundException(string message = "The requested resource was not found.")
    : AppException(message, StatusCodes.Status404NotFound, "Not found");

public sealed class ConflictException(string message)
    : AppException(message, StatusCodes.Status409Conflict, "Conflict");

public sealed class AuthenticationFailedException(string message = "Invalid email or password.")
    : AppException(message, StatusCodes.Status401Unauthorized, "Authentication failed");
