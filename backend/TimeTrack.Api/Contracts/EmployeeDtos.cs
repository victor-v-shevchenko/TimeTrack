namespace TimeTrack.Api.Contracts;

public record ActiveEntryDto(long Id, string Description, DateTimeOffset StartedAt);

public record EmployeeDto(int Id, string Name, DateTimeOffset CreatedAt, ActiveEntryDto? ActiveEntry);
