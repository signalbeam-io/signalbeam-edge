namespace SignalBeam.Shared.Infrastructure.Results;

/// <summary>
/// Represents an error that occurred during an operation.
/// </summary>
public sealed record Error
{
    /// <summary>
    /// Gets the unique error code.
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets the error type.
    /// </summary>
    public ErrorType Type { get; init; }

    /// <summary>
    /// Gets additional metadata about the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    private Error(string code, string message, ErrorType type, IReadOnlyDictionary<string, object>? metadata = null)
    {
        Code = code;
        Message = message;
        Type = type;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Validation, metadata);

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static Error NotFound(string code, string message, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.NotFound, metadata);

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    public static Error Conflict(string code, string message, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Conflict, metadata);

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    public static Error Unauthorized(string code, string message, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Unauthorized, metadata);

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static Error Forbidden(string code, string message, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Forbidden, metadata);

    /// <summary>
    /// Creates a failure error.
    /// </summary>
    public static Error Failure(string code, string message, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Failure, metadata);

    /// <summary>
    /// Creates an unexpected error.
    /// </summary>
    public static Error Unexpected(string code, string message, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Unexpected, metadata);

    /// <summary>
    /// Represents no error (default/none).
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
}

/// <summary>
/// Defines error types for categorization.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// No error.
    /// </summary>
    None = 0,

    /// <summary>
    /// Validation error (400 Bad Request).
    /// </summary>
    Validation = 1,

    /// <summary>
    /// Resource not found (404 Not Found).
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// Conflict error (409 Conflict).
    /// </summary>
    Conflict = 3,

    /// <summary>
    /// Unauthorized access (401 Unauthorized).
    /// </summary>
    Unauthorized = 4,

    /// <summary>
    /// Forbidden access (403 Forbidden).
    /// </summary>
    Forbidden = 5,

    /// <summary>
    /// General failure (500 Internal Server Error).
    /// </summary>
    Failure = 6,

    /// <summary>
    /// Unexpected error (500 Internal Server Error).
    /// </summary>
    Unexpected = 7
}
