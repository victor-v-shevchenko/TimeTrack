using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Domain;
using TimeTrack.Api.Services;

namespace TimeTrack.Tests;

public class ReportServiceTests : IAsyncLifetime
{
    static readonly DateTimeOffset Day = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

    readonly ApiFactory _factory = new();
    readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    int _employees;

    public ValueTask InitializeAsync()
    {
        _ = _factory.Server;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    async Task<int> AddEmployeeAsync(params (DateTimeOffset Start, DateTimeOffset? Stop)[] entries)
    {
        var id = 0;
        await _factory.ExecuteDbAsync(async db =>
        {
            var employee = new Employee { Name = $"Employee {++_employees}", CreatedAt = Day };
            foreach (var (start, stop) in entries)
                employee.Entries.Add(new TimeEntry { Description = "Work", StartedAt = start, StoppedAt = stop });
            db.Employees.Add(employee);
            await db.SaveChangesAsync(_ct);
            id = employee.Id;
        });
        return id;
    }

    async Task<ReportDto> ReportAsync(DateTimeOffset from, DateTimeOffset to, int? employeeId = null)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReportService>().GetAsync(from, to, employeeId, _ct);
    }

    static DateTimeOffset At(int hour, int day = 5) => new(2026, 1, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Empty_period_lists_every_employee_with_zero()
    {
        await AddEmployeeAsync();
        await AddEmployeeAsync();

        var report = await ReportAsync(At(0), At(0, 6));

        Assert.Empty(report.Entries);
        Assert.Equal(2, report.Totals.Count);
        Assert.All(report.Totals, t => Assert.Equal(0, t.TotalSeconds));
    }

    [Fact]
    public async Task Empty_period_with_filter_returns_single_zero_total()
    {
        await AddEmployeeAsync((At(8), At(9)));
        var idle = await AddEmployeeAsync();

        var report = await ReportAsync(At(0), At(0, 6), idle);

        Assert.Empty(report.Entries);
        var total = Assert.Single(report.Totals);
        Assert.Equal(idle, total.EmployeeId);
        Assert.Equal(0, total.TotalSeconds);
    }

    [Fact]
    public async Task Entry_is_clipped_at_period_start()
    {
        await AddEmployeeAsync((At(8), At(10)));

        var report = await ReportAsync(At(9), At(0, 6));

        var entry = Assert.Single(report.Entries);
        Assert.Equal(3600, entry.DurationSeconds);
        Assert.Equal(At(8), entry.StartedAt);
        Assert.Equal(At(10), entry.StoppedAt);
        Assert.Equal(3600, Assert.Single(report.Totals).TotalSeconds);
    }

    [Fact]
    public async Task Entry_is_clipped_at_period_end()
    {
        await AddEmployeeAsync((At(23), At(1, 6)));

        var report = await ReportAsync(At(0), At(0, 6));

        Assert.Equal(3600, Assert.Single(report.Entries).DurationSeconds);
    }

    [Fact]
    public async Task Entries_touching_the_period_boundaries_are_excluded()
    {
        await AddEmployeeAsync((At(6), At(8)), (At(12), At(14)));

        var report = await ReportAsync(At(8), At(12));

        Assert.Empty(report.Entries);
        Assert.Equal(0, Assert.Single(report.Totals).TotalSeconds);
    }

    [Fact]
    public async Task Running_entry_counts_up_to_now()
    {
        await AddEmployeeAsync((At(8), null));
        _factory.Clock.SetUtcNow(At(11));

        var report = await ReportAsync(At(0), At(0, 6));

        var entry = Assert.Single(report.Entries);
        Assert.Null(entry.StoppedAt);
        Assert.Equal(3 * 3600, entry.DurationSeconds);
    }

    [Fact]
    public async Task Running_entry_is_clipped_to_period_end()
    {
        await AddEmployeeAsync((At(8), null));
        _factory.Clock.SetUtcNow(At(12));

        var report = await ReportAsync(At(0), At(10));

        Assert.Equal(2 * 3600, Assert.Single(report.Entries).DurationSeconds);
    }

    [Fact]
    public async Task Totals_equal_sum_of_entry_durations_per_employee()
    {
        var a = await AddEmployeeAsync((At(8), At(9)), (At(10), At(12)));
        var b = await AddEmployeeAsync((At(8), At(8).AddMinutes(30)));

        var report = await ReportAsync(At(0), At(0, 6));

        Assert.Equal(3 * 3600, report.Totals.Single(t => t.EmployeeId == a).TotalSeconds);
        Assert.Equal(1800, report.Totals.Single(t => t.EmployeeId == b).TotalSeconds);
        Assert.Equal(3, report.Entries.Count);
        Assert.Equal(report.Entries.OrderBy(e => e.StartedAt).ThenBy(e => e.Id), report.Entries);
    }

    [Fact]
    public async Task Fractional_seconds_are_floored_per_entry()
    {
        await AddEmployeeAsync((At(8), At(8).AddSeconds(10.9)), (At(9), At(9).AddSeconds(10.9)));

        var report = await ReportAsync(At(0), At(0, 6));

        Assert.All(report.Entries, e => Assert.Equal(10, e.DurationSeconds));
        Assert.Equal(20, Assert.Single(report.Totals).TotalSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Period_must_have_positive_length(int hours)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => ReportAsync(At(10), At(10).AddHours(hours)));
        Assert.Equal("to", ex.Field);
    }

    [Fact]
    public async Task Unknown_employee_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ReportAsync(At(0), At(0, 6), 9999));
    }

    [Fact]
    public async Task Non_utc_offsets_give_the_same_result()
    {
        await AddEmployeeAsync((At(8), At(10)));
        var plusTwo = TimeSpan.FromHours(2);

        var utc = await ReportAsync(At(9), At(12));
        var local = await ReportAsync(At(9).ToOffset(plusTwo), At(12).ToOffset(plusTwo));

        Assert.Equal(utc.Entries, local.Entries);
        Assert.Equal(utc.Totals, local.Totals);
        Assert.Equal(utc.From, local.From);
        Assert.Equal(TimeSpan.Zero, local.From.Offset);
    }
}
