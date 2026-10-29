using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Services;

namespace TimeTrack.Api.Endpoints;

public class AppExceptionHandler : IExceptionHandler
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

        // Write directly: IProblemDetailsService declines when Accept excludes JSON, which would
        // let the exception escape as a bodyless 500.
        httpContext.Response.StatusCode = app.Status;
        await httpContext.Response.WriteAsJsonAsync(
            problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    static string ToCamelCase(string field) =>
        field.Length == 0 ? field : char.ToLowerInvariant(field[0]) + field[1..];
}
