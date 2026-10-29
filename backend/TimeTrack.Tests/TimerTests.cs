using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Contracts;

namespace TimeTrack.Tests;

public class TimerTests : IAsyncLifetime
{
    readonly ApiFactory _factory = new();
    readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    HttpClient _client = null!;
    int _employees;

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

    async Task<int> CreateEmployeeAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/employees", new { name = $"Employee {++_employees}" }, _ct);
        return (await HttpAssert.ReadAsync<EmployeeDto>(response, HttpStatusCode.Created)).Id;
    }

    Task<HttpResponseMessage> StartAsync(int id, string? description = "Work") =>
        _client.PostAsJsonAsync($"/api/employees/{id}/timer/start", new { description }, _ct);

    Task<HttpResponseMessage> StopAsync(int id) =>
        _client.PostAsync($"/api/employees/{id}/timer/stop", null, _ct);

    async Task<EmployeeDto[]> ListAsync() =>
        (await _client.GetFromJsonAsync<EmployeeDto[]>("/api/employees", _ct))!;

    [Fact]
    public async Task Start_for_unknown_employee_is_404_even_with_a_blank_description()
    {
        var response = await StartAsync(9999, " ");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Problem_is_returned_when_Accept_excludes_json()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/employees/9999/timer/stop");
        request.Headers.Accept.ParseAdd("text/html");

        var response = await _client.SendAsync(request, _ct);

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Start_advance_stop_gives_one_closed_entry_of_1800_seconds()
    {
        var id = await CreateEmployeeAsync();
        var started = await HttpAssert.ReadAsync<TimeEntryDto>(await StartAsync(id), HttpStatusCode.Created);

        _factory.Clock.Advance(TimeSpan.FromMinutes(30));
        var stopped = await HttpAssert.ReadAsync<TimeEntryDto>(await StopAsync(id), HttpStatusCode.OK);

        Assert.Equal(started.Id, stopped.Id);
        Assert.Equal(1800, (stopped.StoppedAt - stopped.StartedAt)!.Value.TotalSeconds);
        Assert.Null(Assert.Single(await ListAsync()).ActiveEntry);
    }

    [Fact]
    public async Task Running_timer_shows_as_active_entry()
    {
        var id = await CreateEmployeeAsync();
        await StartAsync(id, "Writing docs");

        var active = Assert.Single(await ListAsync()).ActiveEntry;

        Assert.Equal("Writing docs", active!.Description);
        Assert.Equal(_factory.Clock.GetUtcNow(), active.StartedAt);
    }

    [Fact]
    public async Task Restart_creates_second_entry_and_leaves_the_first_unchanged()
    {
        var id = await CreateEmployeeAsync();
        var dayStart = _factory.Clock.GetUtcNow();

        await StartAsync(id, "First");
        _factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var first = await HttpAssert.ReadAsync<TimeEntryDto>(await StopAsync(id), HttpStatusCode.OK);
        _factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await StartAsync(id, "Second");
        _factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await StopAsync(id);

        var from = Uri.EscapeDataString(dayStart.AddHours(-9).ToString("o"));
        var to = Uri.EscapeDataString(dayStart.AddHours(15).ToString("o"));
        var report = (await _client.GetFromJsonAsync<ReportDto>($"/api/reports?from={from}&to={to}", _ct))!;

        Assert.Equal(2, report.Entries.Count);
        Assert.Equal(first.Id, report.Entries[0].Id);
        Assert.Equal(first.StartedAt, report.Entries[0].StartedAt);
        Assert.Equal(first.StoppedAt, report.Entries[0].StoppedAt);
        Assert.Equal(600, report.Entries[0].DurationSeconds);
        Assert.Equal(1200, report.Entries[1].DurationSeconds);
        Assert.Equal(1800, Assert.Single(report.Totals).TotalSeconds);
    }

    [Fact]
    public async Task Double_start_is_409_and_leaves_one_running_entry()
    {
        var id = await CreateEmployeeAsync();
        await StartAsync(id);

        await HttpAssert.ProblemAsync(await StartAsync(id), HttpStatusCode.Conflict);

        await _factory.ExecuteDbAsync(async db =>
            Assert.Equal(1, await db.TimeEntries.CountAsync(x => x.StoppedAt == null, _ct)));
    }

    [Fact]
    public async Task Parallel_starts_give_one_201_and_nine_409()
    {
        var id = await CreateEmployeeAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => StartAsync(id)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(9, conflicts.Count);
        foreach (var conflict in conflicts)
            await HttpAssert.ProblemAsync(conflict, HttpStatusCode.Conflict);
        await _factory.ExecuteDbAsync(async db =>
            Assert.Equal(1, await db.TimeEntries.CountAsync(x => x.StoppedAt == null, _ct)));
    }

    [Fact]
    public async Task Stop_without_running_timer_is_409()
    {
        var id = await CreateEmployeeAsync();

        await HttpAssert.ProblemAsync(await StopAsync(id), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unknown_employee_is_404()
    {
        await HttpAssert.ProblemAsync(await StartAsync(9999), HttpStatusCode.NotFound);
        await HttpAssert.ProblemAsync(await StopAsync(9999), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Blank_description_is_422_with_field_error()
    {
        var id = await CreateEmployeeAsync();

        var problem = await HttpAssert.ProblemAsync(await StartAsync(id, "  "), HttpStatusCode.UnprocessableEntity);

        Assert.Equal(1, problem.GetProperty("errors").GetProperty("description").GetArrayLength());
    }
}
