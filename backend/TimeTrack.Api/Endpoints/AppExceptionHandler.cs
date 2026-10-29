using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Services;

namespace TimeTrack.Api.Endpoints;

public class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException app)
            return false;

        ProblemDetails problem = app is ValidationException validation
            ? new HttpValidationProblemDetails(
                new Dictionary<string, string[]> { [ToCamelCase(validation.Field)] = [validation.Message] })
            : new ProblemDetails();
        problem.Status = app.Status;
        problem.Title = app.Title;
        problem.Detail = app.Message;

        httpContext.Response.StatusCode = app.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    static string ToCamelCase(string field) =>
        field.Length == 0 ? field : char.ToLowerInvariant(field[0]) + field[1..];
}
