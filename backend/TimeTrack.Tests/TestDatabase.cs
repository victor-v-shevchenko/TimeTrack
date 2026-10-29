using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TimeTrack.Tests;

public static class TestDatabase
{
    public static string BaseConnectionString { get; } = Load();

    public static string NewConnectionString(string dbName) =>
        new NpgsqlConnectionStringBuilder(BaseConnectionString) { Database = dbName }.ConnectionString;

    public static string NewName() => $"timetrack_test_{Guid.NewGuid():N}";

    static string Load()
    {
        var cs = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddKeyPerFile("/run/secrets", optional: true)
            .Build()
            .GetConnectionString("Default");

        return string.IsNullOrWhiteSpace(cs)
            ? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Provide /run/secrets/ConnectionStrings__Default " +
                "or the ConnectionStrings__Default environment variable.")
            : cs;
    }
}
