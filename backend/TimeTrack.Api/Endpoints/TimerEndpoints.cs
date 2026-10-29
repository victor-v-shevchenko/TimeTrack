using Microsoft.AspNetCore.Http.HttpResults;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Services;

namespace TimeTrack.Api.Endpoints;

public static class TimerEndpoints
{
    public static RouteGroupBuilder MapTimerEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/employees/{id:int}/timer/start", async Task<Created<TimeEntryDto>> (
            int id, StartTimerRequest request, TimerService service, CancellationToken ct) =>
        {
            var entry = await service.StartAsync(id, request.Description, ct);
            return TypedResults.Created($"/api/employees/{id}/timer", entry);
        });

        api.MapPost("/employees/{id:int}/timer/stop", async Task<Ok<TimeEntryDto>> (
            int id, TimerService service, CancellationToken ct) =>
            TypedResults.Ok(await service.StopAsync(id, ct)));

        return api;
    }
}
