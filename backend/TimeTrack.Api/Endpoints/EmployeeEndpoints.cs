using Microsoft.AspNetCore.Http.HttpResults;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Services;

namespace TimeTrack.Api.Endpoints;

public static class EmployeeEndpoints
{
    public static RouteGroupBuilder MapEmployeeEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/employees", async Task<Created<EmployeeDto>> (
            CreateEmployeeRequest request, EmployeeService service, CancellationToken ct) =>
        {
            var employee = await service.CreateAsync(request.Name, ct);
            return TypedResults.Created($"/api/employees/{employee.Id}", employee);
        });

        api.MapGet("/employees", async Task<Ok<IReadOnlyList<EmployeeDto>>> (
            EmployeeService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(ct)));

        return api;
    }
}
