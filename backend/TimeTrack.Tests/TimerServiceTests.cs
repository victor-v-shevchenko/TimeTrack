using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Data;
using TimeTrack.Api.Services;

namespace TimeTrack.Tests;

public class TimerServiceTests : IAsyncLifetime
{
    readonly ApiFactory _factory = new();
    readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    int _employees;

    public ValueTask InitializeAsync()
    {
        _ = _factory.Server;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    async Task<T> WithServiceAsync<T, TService>(Func<TService, Task<T>> action) where TService : notnull
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    async Task<int> CreateEmployeeAsync() =>
        (await WithServiceAsync<EmployeeDto, EmployeeService>(s => s.CreateAsync($"Employee {++_employees}", _ct))).Id;

    Task<TimeEntryDto> StartAsync(int employeeId, string? description = "Work") =>
        WithServiceAsync<TimeEntryDto, TimerService>(s => s.StartAsync(employeeId, description, _ct));

    Task<TimeEntryDto> StopAsync(int employeeId) =>
        WithServiceAsync<TimeEntryDto, TimerService>(s => s.StopAsync(employeeId, _ct));

    async Task<int> CountRunningAsync(int employeeId)
    {
        var count = 0;
        await _factory.ExecuteDbAsync(async db =>
            count = await db.TimeEntries.CountAsync(x => x.EmployeeId == employeeId && x.StoppedAt == null, _ct));
        return count;
    }

    [Fact]
    public async Task Start_then_stop_records_one_entry()
    {
        var id = await CreateEmployeeAsync();
        var started = await StartAsync(id);
        Assert.Null(started.StoppedAt);

        _factory.Clock.Advance(TimeSpan.FromMinutes(30));
        var stopped = await StopAsync(id);

        Assert.Equal(started.Id, stopped.Id);
        Assert.Equal(TimeSpan.FromMinutes(30), stopped.StoppedAt - stopped.StartedAt);
        await _factory.ExecuteDbAsync(async db => Assert.Single(await db.TimeEntries.ToListAsync(_ct)));
    }

    [Fact]
    public async Task Restart_creates_a_new_entry_and_keeps_the_old_one()
    {
        var id = await CreateEmployeeAsync();
        var dayStart = _factory.Clock.GetUtcNow();

        await StartAsync(id, "First");
        _factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var first = await StopAsync(id);
        _factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await StartAsync(id, "Second");
        _factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await StopAsync(id);

        await _factory.ExecuteDbAsync(async db =>
        {
            var entries = await db.TimeEntries.OrderBy(x => x.Id).ToListAsync(_ct);
            Assert.Equal(2, entries.Count);
            Assert.Equal(first.Id, entries[0].Id);
            Assert.Equal(first.StartedAt, entries[0].StartedAt);
            Assert.Equal(first.StoppedAt, entries[0].StoppedAt);
            Assert.Equal("First", entries[0].Description);
        });

        var report = await WithServiceAsync<ReportDto, ReportService>(
            s => s.GetAsync(dayStart.AddHours(-9), dayStart.AddHours(15), null, _ct));
        Assert.Equal(1800, Assert.Single(report.Totals).TotalSeconds);
    }

    [Fact]
    public async Task Double_start_is_a_conflict()
    {
        var id = await CreateEmployeeAsync();
        await StartAsync(id);

        await Assert.ThrowsAsync<ConflictException>(() => StartAsync(id));
        Assert.Equal(1, await CountRunningAsync(id));
    }

    [Fact]
    public async Task Parallel_starts_let_exactly_one_win()
    {
        var id = await CreateEmployeeAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await Task.Yield();
            try
            {
                await StartAsync(id);
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }));

        Assert.Equal(1, results.Count(x => x is null));
        Assert.Equal(9, results.Count(x => x is ConflictException));
        Assert.Equal(1, await CountRunningAsync(id));
    }

    [Fact]
    public async Task Stop_without_running_timer_is_a_conflict()
    {
        var id = await CreateEmployeeAsync();
        await Assert.ThrowsAsync<ConflictException>(() => StopAsync(id));
    }

    [Fact]
    public async Task Unknown_employee_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => StartAsync(9999));
        await Assert.ThrowsAsync<NotFoundException>(() => StopAsync(9999));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_description_is_rejected(string? description)
    {
        var id = await CreateEmployeeAsync();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => StartAsync(id, description));
        Assert.Equal("description", ex.Field);
    }

    [Fact]
    public async Task Description_is_trimmed_and_limited_to_500()
    {
        var id = await CreateEmployeeAsync();
        await Assert.ThrowsAsync<ValidationException>(() => StartAsync(id, new string('x', 501)));

        var entry = await StartAsync(id, "  Work  ");
        Assert.Equal("Work", entry.Description);
    }
}
