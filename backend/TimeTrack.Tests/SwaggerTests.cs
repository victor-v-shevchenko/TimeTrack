using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace TimeTrack.Tests;

public class SwaggerTests
{
    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task Swagger_is_only_available_in_development(string environment, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory();
        await using var configured = factory.WithWebHostBuilder(b => b.UseEnvironment(environment));
        using var client = configured.CreateClient();

        var ui = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);
        var spec = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(expected, ui.StatusCode);
        Assert.Equal(expected, spec.StatusCode);
    }
}
