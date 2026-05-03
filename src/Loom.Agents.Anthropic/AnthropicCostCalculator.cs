using Loom.Domain.Runs;

namespace Loom.Agents.Anthropic;

/// <summary>
/// Per-1M-token prices for Anthropic models, in USD. Source: Anthropic docs
/// at the time of writing. The pricing table is intentionally narrow: only
/// the models we route to in Phase 1. Update when adding a new model and
/// when prices change; an outdated price is a budget bug.
/// </summary>
internal static class AnthropicCostCalculator
{
    private sealed record Pricing(decimal InputPerMillion, decimal OutputPerMillion);

    private static readonly Dictionary<string, Pricing> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        // Opus 4.x
        ["claude-opus-4-7"] = new(15m, 75m),
        ["claude-opus-4-6"] = new(15m, 75m),
        // Sonnet 4.x
        ["claude-sonnet-4-6"] = new(3m, 15m),
        ["claude-sonnet-4-5"] = new(3m, 15m),
        // Haiku 4.x
        ["claude-haiku-4-5-20251001"] = new(1m, 5m),
        ["claude-haiku-4-5"] = new(1m, 5m)
    };

    public static Cost Compute(string model, int inputTokens, int outputTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (!Models.TryGetValue(model, out var pricing))
        {
            // Unknown model: log nothing, return zero-cost. Phase 5's router
            // refuses to route to a model with no pricing entry; this guard
            // keeps the runtime usable for ad-hoc dev models in the meantime.
            return new Cost(model, null, inputTokens, outputTokens, 0m);
        }
        var input = pricing.InputPerMillion * inputTokens / 1_000_000m;
        var output = pricing.OutputPerMillion * outputTokens / 1_000_000m;
        return new Cost(model, null, inputTokens, outputTokens, decimal.Round(input + output, 6));
    }
}
