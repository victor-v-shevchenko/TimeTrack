using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeTrack.Api.Data;
using TimeTrack.Api.Domain;

namespace TimeTrack.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task Data_survives_restart()
    {
        var dbName = TestDatabase.NewName();
        var start = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);

        var a = new ApiFactory(dbName) { DropDatabaseOnDispose = false };
        try
        {
            _ = a.Server; // boot the host so migrations run
            await a.ExecuteDbAsync(async db =>
            {
                var employee = new Employee { Name = "Alice Example", CreatedAt = start };
                employee.Entries.Add(new TimeEntry
                {
                    Description = "Closed", StartedAt = start, StoppedAt = start.AddHours(2),
                });
                employee.Entries.Add(new TimeEntry
                {
                    Description = "Running", StartedAt = start.AddHours(3),
                });
                db.Employees.Add(employee);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            });
        }
        finally
        {
            await a.DisposeAsync();
        }

        await using var b = new ApiFactory(dbName);
        _ = b.Server;
        await b.ExecuteDbAsync(async db =>
        {
            var employee = await db.Employees.Include(x => x.Entries).SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Alice Example", employee.Name);
            Assert.Equal(start, employee.CreatedAt);

            var closed = employee.Entries.Single(x => x.Description == "Closed");
            Assert.Equal(start, closed.StartedAt);
            Assert.Equal(start.AddHours(2), closed.StoppedAt);

            var running = employee.Entries.Single(x => x.Description == "Running");
            Assert.Equal(start.AddHours(3), running.StartedAt);
            Assert.Null(running.StoppedAt);
        });
    }

    [Fact]
    public async Task Partial_index_allows_one_running_entry_per_employee()
    {
        await using var factory = new ApiFactory();
        _ = factory.Server;
        var start = factory.Clock.GetUtcNow();

        await factory.ExecuteDbAsync(async db =>
        {
            var employee = new Employee { Name = "Bob Example", CreatedAt = start };
            employee.Entries.Add(new TimeEntry { Description = "Done 1", StartedAt = start, StoppedAt = start.AddHours(1) });
            employee.Entries.Add(new TimeEntry { Description = "Done 2", StartedAt = start.AddHours(2), StoppedAt = start.AddHours(3) });
            employee.Entries.Add(new TimeEntry { Description = "Running", StartedAt = start.AddHours(4) });
            db.Employees.Add(employee);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await factory.ExecuteDbAsync(async db =>
        {
            var employeeId = await db.Employees.Select(x => x.Id).SingleAsync(TestContext.Current.CancellationToken);
            db.TimeEntries.Add(new TimeEntry { EmployeeId = employeeId, Description = "Second running", StartedAt = start.AddHours(5) });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
            var pg = Assert.IsType<PostgresException>(ex.InnerException);
            Assert.Equal("23505", pg.SqlState);
            Assert.Equal(AppDbContext.OneRunningIndex, pg.ConstraintName);
        });
    }

    [Fact]
    public async Task Missing_connection_string_fails_fast()
    {
        await using var factory = new ApiFactory(connectionString: "") { DropDatabaseOnDispose = false };

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("ConnectionStrings:Default", ex.Message);
    }
}
