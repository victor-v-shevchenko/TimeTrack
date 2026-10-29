using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
    options.UseNpgsql(cs).UseSnakeCaseNamingConvention();
    if (sp.GetRequiredService<IHostEnvironment>().IsDevelopment())
        options.EnableSensitiveDataLogging();
});

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
    throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. Provide /run/secrets/ConnectionStrings__Default " +
        "or the ConnectionStrings__Default environment variable.");

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.Run();

public partial class Program;
