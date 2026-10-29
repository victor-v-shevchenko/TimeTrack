using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Data;
using TimeTrack.Api.Domain;

namespace TimeTrack.Api.Services;

public class TimerService(AppDbContext db, TimeProvider clock)
{
    const int MaxDescriptionLength = 500;

    public async Task<TimeEntryDto> StartAsync(int employeeId, string? description, CancellationToken ct = default)
    {
        await EnsureEmployeeExistsAsync(employeeId, ct);

        description = description?.Trim();
        if (string.IsNullOrEmpty(description))
            throw new ValidationException("description", "Description is required.");
        if (description.Length > MaxDescriptionLength)
            throw new ValidationException("description", $"Description must be at most {MaxDescriptionLength} characters.");

        var alreadyRunning = new ConflictException("Timer is already running for this employee.");
        if (await db.TimeEntries.AnyAsync(x => x.EmployeeId == employeeId && x.StoppedAt == null, ct))
            throw alreadyRunning;

        var entry = new TimeEntry
        {
            EmployeeId = employeeId,
            Description = description,
            StartedAt = clock.GetUtcNow(),
        };
        db.TimeEntries.Add(entry);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AppDbContext.OneRunningIndex,
        })
        {
            throw alreadyRunning;
        }

        return ToDto(entry);
    }

    public async Task<TimeEntryDto> StopAsync(int employeeId, CancellationToken ct = default)
    {
        await EnsureEmployeeExistsAsync(employeeId, ct);

        var entry = await db.TimeEntries.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.StoppedAt == null, ct)
            ?? throw new ConflictException("No running timer for this employee.");

        entry.StoppedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    async Task EnsureEmployeeExistsAsync(int employeeId, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct))
            throw new NotFoundException($"Employee {employeeId} not found.");
    }

    static TimeEntryDto ToDto(TimeEntry e) =>
        new(e.Id, e.EmployeeId, e.Description, e.StartedAt, e.StoppedAt);
}
