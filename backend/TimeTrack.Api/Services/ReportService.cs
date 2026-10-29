using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Data;

namespace TimeTrack.Api.Services;

public class ReportService(AppDbContext db, TimeProvider clock)
{
    public async Task<ReportDto> GetAsync(
        DateTimeOffset from, DateTimeOffset to, int? employeeId, CancellationToken ct = default)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (from >= to)
            throw new ValidationException("to", "'to' must be after 'from'.");

        if (employeeId is { } id && !await db.Employees.AnyAsync(x => x.Id == id, ct))
            throw new NotFoundException($"Employee {id} not found.");

        var now = clock.GetUtcNow();

        var rows = await db.TimeEntries
            .AsNoTracking()
            .Where(x => x.StartedAt < to && (x.StoppedAt ?? now) > from)
            .Where(x => employeeId == null || x.EmployeeId == employeeId)
            .Select(x => new { x.Id, x.EmployeeId, x.Employee.Name, x.Description, x.StartedAt, x.StoppedAt })
            .ToListAsync(ct);

        var entries = rows
            .OrderBy(x => x.StartedAt).ThenBy(x => x.Id)
            .Select(x =>
            {
                var start = x.StartedAt > from ? x.StartedAt : from;
                var stop = x.StoppedAt ?? now;
                var end = stop < to ? stop : to;
                var seconds = Math.Max(0, (long)Math.Floor((end - start).TotalSeconds));
                return new ReportEntryDto(
                    x.Id, x.EmployeeId, x.Name, x.Description, x.StartedAt, x.StoppedAt, seconds);
            })
            .ToList();

        var secondsByEmployee = entries
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.DurationSeconds));

        var employees = await db.Employees
            .AsNoTracking()
            .Where(x => employeeId == null || x.Id == employeeId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        var totals = employees
            .Select(x => new ReportTotalDto(x.Id, x.Name, secondsByEmployee.GetValueOrDefault(x.Id)))
            .ToList();

        return new ReportDto(from, to, entries, totals);
    }
}
