using FluentAssertions;
using Xunit;

namespace Loom.Web.Tests;

public sealed class HostSmokeTests : IClassFixture<LoomWebFactory>
{
    private readonly LoomWebFactory _factory;

    public HostSmokeTests(LoomWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Healthz_Responds_Ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"ok\"");
    }

    [Fact]
    public async Task Home_RendersOperatingPicture_InDevModeWithoutAuth()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Operating");
        // The empty-state message renders when no project is seeded.
        html.Should().Contain("No project yet");
    }

    [Fact]
    public async Task UnknownRoute_RendersNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/this-route-does-not-exist");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("doesn't exist");
    }
}
