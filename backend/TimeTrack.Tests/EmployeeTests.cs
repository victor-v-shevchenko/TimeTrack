using System.Net;
using System.Net.Http.Json;
using TimeTrack.Api.Contracts;

namespace TimeTrack.Tests;

public class EmployeeTests : IAsyncLifetime
{
    readonly ApiFactory _factory = new();
    readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_appears_in_list()
    {
        var response = await _client.PostAsJsonAsync("/api/employees", new { name = "  Alice Example  " }, _ct);
        var created = await HttpAssert.ReadAsync<EmployeeDto>(response, HttpStatusCode.Created);

        Assert.Equal("Alice Example", created.Name);
        Assert.Equal($"/api/employees/{created.Id}", response.Headers.Location?.ToString());

        var list = await _client.GetFromJsonAsync<EmployeeDto[]>("/api/employees", _ct);
        var listed = Assert.Single(list!);
        Assert.Equal(created.Id, listed.Id);
        Assert.Null(listed.ActiveEntry);

        var raw = await _client.GetStringAsync("/api/employees", _ct);
        Assert.Contains("\"activeEntry\":null", raw);
    }

    [Fact]
    public async Task Blank_name_is_422_with_field_error()
    {
        var response = await _client.PostAsJsonAsync("/api/employees", new { name = "  " }, _ct);

        var problem = await HttpAssert.ProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal(1, problem.GetProperty("errors").GetProperty("name").GetArrayLength());
    }

    [Fact]
    public async Task Duplicate_name_is_409()
    {
        (await _client.PostAsJsonAsync("/api/employees", new { name = "Bob Example" }, _ct)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/employees", new { name = "Bob Example" }, _ct);

        await HttpAssert.ProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unknown_route_is_a_problem_response()
    {
        var response = await _client.GetAsync("/api/nope", _ct);

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }
}
