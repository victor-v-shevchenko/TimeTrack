using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeTrack.Api.Contracts;
using TimeTrack.Api.Data;
using TimeTrack.Api.Domain;

namespace TimeTrack.Api.Services;

public class EmployeeService(AppDbContext db, TimeProvider clock)
{
    const int MaxNameLength = 100;

    public async Task<EmployeeDto> CreateAsync(string? name, CancellationToken ct = default)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new ValidationException("name", "Name is required.");
        if (name.Length > MaxNameLength)
            throw new ValidationException("name", $"Name must be at most {MaxNameLength} characters.");

        var duplicate = new ConflictException("An employee with this name already exists.");
        if (await db.Employees.AnyAsync(x => x.Name == name, ct))
            throw duplicate;

        var employee = new Employee { Name = name, CreatedAt = clock.GetUtcNow() };
        db.Employees.Add(employee);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AppDbContext.EmployeeNameIndex,
        })
        {
            throw duplicate;
        }

        return new EmployeeDto(employee.Id, employee.Name, employee.CreatedAt, null);
    }

    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(CancellationToken ct = default) =>
        await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new EmployeeDto(
                e.Id, e.Name, e.CreatedAt,
                e.Entries
                    .Where(t => t.StoppedAt == null)
                    .Select(t => new ActiveEntryDto(t.Id, t.Description, t.StartedAt))
                    .FirstOrDefault()))
            .ToListAsync(ct);
}
