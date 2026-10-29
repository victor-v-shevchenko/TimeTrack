using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Api.Services;

namespace TimeTrack.Tests;

public class EmployeeServiceTests : IAsyncLifetime
{
    readonly ApiFactory _factory = new();
    readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _ = _factory.Server;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    async Task<T> WithServiceAsync<T>(Func<EmployeeService, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<EmployeeService>());
    }

    [Fact]
    public async Task Create_trims_name_and_list_returns_it_without_active_entry()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("  Alice Example  ", _ct));
        Assert.Equal("Alice Example", created.Name);
        Assert.Equal(_factory.Clock.GetUtcNow(), created.CreatedAt);

        var list = await WithServiceAsync(s => s.ListAsync(_ct));
        var listed = Assert.Single(list);
        Assert.Equal(created.Id, listed.Id);
        Assert.Equal("Alice Example", listed.Name);
        Assert.Null(listed.ActiveEntry);
    }

    [Fact]
    public async Task List_is_ordered_by_name_and_includes_running_entry()
    {
        var zed = await WithServiceAsync(s => s.CreateAsync("Zed Example", _ct));
        await WithServiceAsync(s => s.CreateAsync("Amy Example", _ct));

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<TimerService>().StartAsync(zed.Id, "Work", _ct);

        var list = await WithServiceAsync(s => s.ListAsync(_ct));
        Assert.Equal(["Amy Example", "Zed Example"], list.Select(x => x.Name));
        Assert.Null(list[0].ActiveEntry);
        Assert.Equal("Work", list[1].ActiveEntry!.Description);
        Assert.Equal(_factory.Clock.GetUtcNow(), list[1].ActiveEntry!.StartedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_name_is_rejected(string? name)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => WithServiceAsync(s => s.CreateAsync(name, _ct)));
        Assert.Equal("name", ex.Field);
    }

    [Fact]
    public async Task Name_length_limit_is_100()
    {
        await WithServiceAsync(s => s.CreateAsync(new string('a', 100), _ct));
        await Assert.ThrowsAsync<ValidationException>(() => WithServiceAsync(s => s.CreateAsync(new string('b', 101), _ct)));
    }

    [Fact]
    public async Task Duplicate_name_is_a_conflict()
    {
        await WithServiceAsync(s => s.CreateAsync("Bob Example", _ct));
        await Assert.ThrowsAsync<ConflictException>(() => WithServiceAsync(s => s.CreateAsync(" Bob Example ", _ct)));
    }
}
