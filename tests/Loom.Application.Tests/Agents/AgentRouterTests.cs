using FluentAssertions;
using Loom.Application.Agents;
using Loom.Domain.Runs;
using Xunit;

namespace Loom.Application.Tests.Agents;

public sealed class AgentRouterTests
{
    [Fact]
    public void Resolve_returns_runtime_for_engine()
    {
        var rt = new FakeRuntime(EngineName.Anthropic);
        var router = new DefaultAgentRouter([rt]);
        router.Resolve(EngineName.Anthropic).Should().BeSameAs(rt);
    }

    [Fact]
    public void Resolve_throws_for_unregistered_engine()
    {
        var router = new DefaultAgentRouter([new FakeRuntime(EngineName.Anthropic)]);
        var act = () => router.Resolve(EngineName.Foundry);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveWithFallback_picks_first_healthy_registered_engine()
    {
        var foundry = new FakeRuntime(EngineName.Foundry);
        var anthropic = new FakeRuntime(EngineName.Anthropic);
        var health = new InMemoryEngineHealthMonitor();
        // Mark Foundry unhealthy by recording many failures.
        for (var i = 0; i < 10; i++) health.RecordFailure(EngineName.Foundry);

        var router = new DefaultAgentRouter([foundry, anthropic], health);
        var picked = router.ResolveWithFallback([EngineName.Foundry, EngineName.Anthropic]);
        picked.Should().BeSameAs(anthropic);
    }

    [Fact]
    public void ResolveWithFallback_returns_first_registered_when_all_unhealthy()
    {
        var foundry = new FakeRuntime(EngineName.Foundry);
        var health = new InMemoryEngineHealthMonitor();
        for (var i = 0; i < 10; i++) health.RecordFailure(EngineName.Foundry);

        var router = new DefaultAgentRouter([foundry], health);
        var picked = router.ResolveWithFallback([EngineName.Foundry]);
        picked.Should().BeSameAs(foundry);
    }

    [Fact]
    public void ResolveWithFallback_throws_when_no_preferred_registered()
    {
        var router = new DefaultAgentRouter([new FakeRuntime(EngineName.InProc)]);
        var act = () => router.ResolveWithFallback([EngineName.Foundry, EngineName.Anthropic]);
        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class FakeRuntime(EngineName engine) : IAgentRuntime
    {
        public EngineName Engine { get; } = engine;
        public EngineCapabilities Capabilities { get; } = new(true, true, true, true, 200_000);
        public Task<string> StartAsync(AgentRunRequest request, CancellationToken ct = default) =>
            Task.FromResult("ext");
        public Task CancelAsync(string externalRunId, CancellationToken ct = default) => Task.CompletedTask;
        public async IAsyncEnumerable<AgentRunEvent> StreamEventsAsync(string externalRunId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
