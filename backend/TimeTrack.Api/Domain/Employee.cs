namespace TimeTrack.Api.Domain;

public class Employee
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<TimeEntry> Entries { get; } = [];
}
