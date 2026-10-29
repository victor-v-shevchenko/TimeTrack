namespace TimeTrack.Api.Contracts;

public record ReportEntryDto(
    long Id, int EmployeeId, string EmployeeName, string Description,
    DateTimeOffset StartedAt, DateTimeOffset? StoppedAt, long DurationSeconds);

public record ReportTotalDto(int EmployeeId, string EmployeeName, long TotalSeconds);

public record ReportDto(
    DateTimeOffset From, DateTimeOffset To,
    IReadOnlyList<ReportEntryDto> Entries, IReadOnlyList<ReportTotalDto> Totals);
