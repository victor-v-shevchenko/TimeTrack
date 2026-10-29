namespace TimeTrack.Api.Contracts;

public record CreateEmployeeRequest(string? Name);

public record StartTimerRequest(string? Description);
