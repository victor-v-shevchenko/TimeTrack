using System.Net;
using System.Net.Http.Json;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Domain;

namespace TimeTrack.Tests;

public class ReportTests : IAsyncLifetime
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

    static DateTimeOffset At(int hour, int day = 5) => new(2026, 1, day, hour, 0, 0, TimeSpan.Zero);

    static string Iso(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("o"));

    async Task<int> AddEmployeeAsync(params (DateTimeOffset Start, DateTimeOffset? Stop)[] entries)
    {
        var id = 0;
        await _factory.ExecuteDbAsync(async db =>
        {
            var employee = new Employee { Name = $"Employee {++_employees}", CreatedAt = At(0) };
            foreach (var (start, stop) in entries)
                employee.Entries.Add(new TimeEntry { Description = "Work", StartedAt = start, StoppedAt = stop });
            db.Employees.Add(employee);
            await db.SaveChangesAsync(_ct);
            id = employee.Id;
        });
        return id;
    }

    Task<HttpResponseMessage> GetAsync(DateTimeOffset from, DateTimeOffset to, int? employeeId = null) =>
        _client.GetAsync($"/api/reports?from={Iso(from)}&to={Iso(to)}" + (employeeId is { } id ? $"&employeeId={id}" : ""), _ct);

    [Fact]
    public async Task Empty_report_lists_every_employee_with_zero()
    {
        await AddEmployeeAsync();
        await AddEmployeeAsync();

        var response = await GetAsync(At(0), At(0, 6));

        var report = await HttpAssert.ReadAsync<ReportDto>(response, HttpStatusCode.OK);
        Assert.Empty(report.Entries);
        Assert.Equal(2, report.Totals.Count);
        Assert.All(report.Totals, t => Assert.Equal(0, t.TotalSeconds));
    }

    [Fact]
    public async Task Empty_report_filtered_by_employee_without_entries_has_one_zero_total()
    {
        await AddEmployeeAsync((At(8), At(9)));
        var idle = await AddEmployeeAsync();

        var report = await HttpAssert.ReadAsync<ReportDto>(await GetAsync(At(0), At(0, 6), idle), HttpStatusCode.OK);

        Assert.Empty(report.Entries);
        var total = Assert.Single(report.Totals);
        Assert.Equal(idle, total.EmployeeId);
        Assert.Equal(0, total.TotalSeconds);
    }

    [Fact]
    public async Task Entry_crossing_the_period_boundary_is_clipped()
    {
        await AddEmployeeAsync((At(23), At(1, 6)));

        var report = await HttpAssert.ReadAsync<ReportDto>(await GetAsync(At(0), At(0, 6)), HttpStatusCode.OK);

        Assert.Equal(3600, Assert.Single(report.Entries).DurationSeconds);
        Assert.Equal(3600, Assert.Single(report.Totals).TotalSeconds);
    }

    [Fact]
    public async Task Running_entry_is_counted_up_to_now()
    {
        await AddEmployeeAsync((At(8), null));
        _factory.Clock.SetUtcNow(At(11));

        var report = await HttpAssert.ReadAsync<ReportDto>(await GetAsync(At(0), At(0, 6)), HttpStatusCode.OK);

        var entry = Assert.Single(report.Entries);
        Assert.Null(entry.StoppedAt);
        Assert.Equal(3 * 3600, entry.DurationSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task From_not_before_to_is_422(int hours)
    {
        var response = await GetAsync(At(10), At(10).AddHours(hours));

        var problem = await HttpAssert.ProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal(1, problem.GetProperty("errors").GetProperty("to").GetArrayLength());
    }

    [Fact]
    public async Task Missing_from_is_400()
    {
        var response = await _client.GetAsync($"/api/reports?to={Iso(At(0, 6))}", _ct);

        await HttpAssert.ProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_employee_is_404()
    {
        await HttpAssert.ProblemAsync(await GetAsync(At(0), At(0, 6), 9999), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Plus_offsets_in_the_query_are_treated_correctly()
    {
        await AddEmployeeAsync((At(8), At(10)));
        var plusTwo = TimeSpan.FromHours(2);

        // 11:00+02:00 .. 14:00+02:00 is 09:00Z .. 12:00Z
        var response = await GetAsync(At(9).ToOffset(plusTwo), At(12).ToOffset(plusTwo));

        var report = await HttpAssert.ReadAsync<ReportDto>(response, HttpStatusCode.OK);
        Assert.Equal(At(9), report.From);
        Assert.Equal(3600, Assert.Single(report.Entries).DurationSeconds);
    }
}
