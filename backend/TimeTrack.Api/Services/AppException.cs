namespace TimeTrack.Api.Services;

public abstract class AppException(int status, string title, string detail) : Exception(detail)
{
    public int Status { get; } = status;
    public string Title { get; } = title;
}

public sealed class NotFoundException(string detail) : AppException(404, "Not found", detail);

public sealed class ConflictException(string detail) : AppException(409, "Conflict", detail);

public sealed class ValidationException(string field, string message)
    : AppException(422, "Validation failed", message)
{
    public string Field { get; } = field;
}
