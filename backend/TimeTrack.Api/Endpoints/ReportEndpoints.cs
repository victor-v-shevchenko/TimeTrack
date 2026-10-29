using Microsoft.AspNetCore.Http.HttpResults;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Services;

namespace TimeTrack.Api.Endpoints;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/reports", async Task<Ok<ReportDto>> (
            DateTimeOffset from, DateTimeOffset to, int? employeeId, ReportService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(from, to, employeeId, ct)));

        return api;
    }
}
