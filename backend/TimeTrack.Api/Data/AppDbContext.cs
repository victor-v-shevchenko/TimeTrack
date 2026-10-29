using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Domain;

namespace TimeTrack.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const string OneRunningIndex = "ux_time_entries_one_running";
    public const string EmployeeNameIndex = "ux_employees_name";

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName(EmployeeNameIndex);
            e.ToTable(t => t.HasCheckConstraint("ck_employees_name_not_blank", "btrim(name) <> ''"));
        });

        modelBuilder.Entity<TimeEntry>(e =>
        {
            e.Property(x => x.Description).IsRequired().HasMaxLength(500);
            e.HasOne(x => x.Employee).WithMany(x => x.Entries)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.EmployeeId)
                .IsUnique()
                .HasFilter("stopped_at IS NULL")
                .HasDatabaseName(OneRunningIndex);
            e.HasIndex(x => x.StartedAt).HasDatabaseName("ix_time_entries_started_at");
            e.ToTable(t => t.HasCheckConstraint(
                "ck_time_entries_stop_after_start", "stopped_at IS NULL OR stopped_at >= started_at"));
        });
    }
}
