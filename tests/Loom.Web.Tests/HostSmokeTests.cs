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
        // Pages use InteractiveServer with prerender:false, so the initial
        // GET returns the layout chrome plus a Blazor placeholder for the
        // page component. Assert on the layout markers that are always SSR'd.
        html.Should().Contain("Operating picture");
        html.Should().Contain("proj-pill");
    }

    [Fact]
    public async Task UnknownRoute_RendersNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/this-route-does-not-exist");
        // With InteractiveServer + prerender:false, the <NotFound> body is
        // not server-rendered; the host returns a bare 404 status.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
