namespace TimeTrack.Api.Contracts;

public record TimeEntryDto(
    long Id, int EmployeeId, string Description, DateTimeOffset StartedAt, DateTimeOffset? StoppedAt);
