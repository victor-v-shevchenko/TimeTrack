# Step 2: Backend foundation — domain, DbContext, configuration, first migration

## Goal
An API project that reads its connection string from the secret, applies migrations on
startup, and stores employees and time entries. A test proves the data survives an app restart.

## Depends on
Step 1.

## Files

    backend/TimeTrack.slnx
    backend/TimeTrack.Api/TimeTrack.Api.csproj
    backend/TimeTrack.Api/Program.cs
    backend/TimeTrack.Api/appsettings.json               # logging only, no connection string
    backend/TimeTrack.Api/appsettings.Development.json
    backend/TimeTrack.Api/Domain/Employee.cs
    backend/TimeTrack.Api/Domain/TimeEntry.cs
    backend/TimeTrack.Api/Data/AppDbContext.cs
    backend/TimeTrack.Api/Data/Migrations/*               # generated: Initial
    backend/TimeTrack.Tests/TimeTrack.Tests.csproj
    backend/TimeTrack.Tests/ApiFactory.cs
    backend/TimeTrack.Tests/TestDatabase.cs
    backend/TimeTrack.Tests/PersistenceTests.cs

`dotnet new sln` produces `.slnx` on .NET 10. Keep it; PLAN.md layout is updated accordingly.

## Packages
- Api: `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`,
  `Microsoft.EntityFrameworkCore.Design` (PrivateAssets=all),
  `Microsoft.Extensions.Configuration.KeyPerFile`.
- Tests: `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
  `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing`.

Use the latest stable versions compatible with .NET 10. Enable `<Nullable>` and
`<TreatWarningsAsErrors>` in both projects.

## Domain

    public class Employee
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<TimeEntry> Entries { get; } = [];
    }

    public class TimeEntry
    {
        public long Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public required string Description { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? StoppedAt { get; set; }
    }

## AppDbContext (`OnModelCreating`)
- `Employee.Name`: required, max length 100, unique index `ux_employees_name`.
- `TimeEntry.Description`: required, max length 500.
- FK `time_entries.employee_id → employees.id`, `OnDelete(Restrict)`.
- Partial unique index:

      e.HasIndex(x => x.EmployeeId)
       .IsUnique()
       .HasFilter("stopped_at IS NULL")
       .HasDatabaseName("ux_time_entries_one_running");

- Index `ix_time_entries_started_at` on `StartedAt` for the report query.
- Check constraint `ck_time_entries_stop_after_start`: `stopped_at IS NULL OR stopped_at >= started_at`.
- Check constraint `ck_employees_name_not_blank`: `btrim(name) <> ''`.
- Expose a `public const string OneRunningIndex = "ux_time_entries_one_running";` (and the
  same for the name index). Step 3 uses them to map violations.

## Program.cs

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
        options.UseNpgsql(cs).UseSnakeCaseNamingConvention();
        if (sp.GetRequiredService<IHostEnvironment>().IsDevelopment())
            options.EnableSensitiveDataLogging();
    });

    var app = builder.Build();

    if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
        throw new InvalidOperationException(
            "ConnectionStrings:Default is not configured. Provide /run/secrets/ConnectionStrings__Default " +
            "or the ConnectionStrings__Default environment variable.");

    using (var scope = app.Services.CreateScope())
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

    app.Run();

    public partial class Program;

Why the connection string is read lazily (`(sp, options) =>`) and checked after `Build()`:
`WebApplicationFactory` applies test configuration during `Build()`. A value read from
`builder.Configuration` before that would still be the secret, not the test database.

## Migration

    dotnet ef migrations add Initial --project backend/TimeTrack.Api --output-dir Data/Migrations

Read the generated SQL (`dotnet ef migrations script`) and confirm the partial index has
`WHERE stopped_at IS NULL` and that all column names are snake_case.

## Test infrastructure

`TestDatabase` (static helper):
- `BaseConnectionString`: built from `new ConfigurationBuilder().AddEnvironmentVariables()
  .AddKeyPerFile("/run/secrets", optional: true).Build().GetConnectionString("Default")`.
  If missing, throw with the same message as Program.
- `NewConnectionString(string dbName)`: `NpgsqlConnectionStringBuilder` with `Database = dbName`.
- `NewName()`: `$"timetrack_test_{Guid.NewGuid():N}"`.

`ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`:
- Constructor takes an optional `databaseName` (default `TestDatabase.NewName()`), so a
  second factory can reuse the same database.
- `public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));`
- `ConfigureWebHost`: `UseEnvironment("Testing")`;
  `ConfigureAppConfiguration(c => c.AddInMemoryCollection([new("ConnectionStrings:Default", cs)]))`;
  `ConfigureTestServices(s => s.AddSingleton<TimeProvider>(Clock))`.
- `DropDatabaseOnDispose` (default true). `DisposeAsync` calls `NpgsqlConnection.ClearAllPools()`,
  then `EnsureDeletedAsync()` through a fresh context.
- Helper `ExecuteDbAsync(Func<AppDbContext, Task>)`, which runs the callback in a new scope.

Test classes use `IClassFixture<ApiFactory>`. Tests that need an isolated database create their own factory.

## PersistenceTests
1. **Data survives restart.** Factory A (`DropDatabaseOnDispose = false`) inserts an employee
   with one closed entry and one running entry (`StoppedAt = null`), then is disposed.
   Factory B on the same database name reads them back: same names, descriptions and timestamps,
   and the running entry still has `StoppedAt == null`. B drops the database.
2. **Partial index works.** Inserting a second running entry for the same employee throws
   `DbUpdateException` whose inner `PostgresException` has `SqlState == "23505"` and
   `ConstraintName == AppDbContext.OneRunningIndex`. Two closed entries are fine.
3. **Missing connection string fails fast.** A factory that overrides the connection string
   with `""`: `CreateClient()` throws `InvalidOperationException` mentioning `ConnectionStrings:Default`.

## Pitfalls
- Npgsql rejects `DateTimeOffset` with a non-zero offset for `timestamptz`. Always use UTC.
- `timestamptz` keeps microseconds and .NET keeps 100 ns ticks. Compare timestamps from the
  fake clock (whole seconds), or truncate before comparing.
- Don't put the connection string in `appsettings*.json`, even for Development.

## Acceptance criteria
- [ ] `dotnet build backend` has no warnings.
- [ ] `dotnet run --project backend/TimeTrack.Api` creates the tables in `timetrack` (snake_case, partial index present).
- [ ] `ConnectionStrings__Default= dotnet run ...` with the secret temporarily hidden shows the clear startup error. Mention this manual check in the commit body.
- [ ] `dotnet test backend`: all PersistenceTests pass, and no `timetrack_test_*` databases are left (`\l`).
- [ ] No connection string in any tracked file.

## Commit
`Step 2: Backend foundation`
