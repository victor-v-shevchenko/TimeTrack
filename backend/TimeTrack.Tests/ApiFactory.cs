using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using TimeTrack.Api.Data;

namespace TimeTrack.Tests;

public class ApiFactory(string? databaseName = null, string? connectionString = null)
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    readonly string _databaseName = databaseName ?? TestDatabase.NewName();

    public ApiFactory() : this(null) { }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));

    public bool DropDatabaseOnDispose { get; set; } = true;

    public string DatabaseName => _databaseName;

    string ConnectionString => connectionString ?? TestDatabase.NewConnectionString(_databaseName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(c =>
            c.AddInMemoryCollection([new("ConnectionStrings:Default", ConnectionString)]));
        builder.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(Clock));
    }

    public async Task ExecuteDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!DropDatabaseOnDispose) return;

        NpgsqlConnection.ClearAllPools();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
        NpgsqlConnection.ClearAllPools();
    }
}
