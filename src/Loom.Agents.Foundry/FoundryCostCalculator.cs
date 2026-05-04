using Loom.Domain.Runs;

namespace Loom.Agents.Foundry;

/// <summary>
/// Per-1M-token prices for Azure OpenAI / Foundry models, in USD. Source:
/// Azure OpenAI pricing page at the time of writing. Foundry's per-token
/// rate matches the public Azure OpenAI list price; the Azure resource may
/// add a deployment surcharge captured at billing-export level, not here.
/// Update when adding a new model and when prices change; an outdated
/// price is a budget bug.
///
/// Cost.Deployment is populated from FoundryOptions.Deployment so a
/// downstream cost report can pivot per-deployment (ADR-0002 commitment).
/// </summary>
internal static class FoundryCostCalculator
{
    private sealed record Pricing(decimal InputPerMillion, decimal OutputPerMillion);

    private static readonly Dictionary<string, Pricing> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        // GPT-5.x family (placeholder values pending confirmed Foundry list
        // prices for the marketplace deployment used in dev — adjust when
        // the team locks the production model).
        ["gpt-5.4"] = new(2.50m, 10.00m),
        ["gpt-5"] = new(2.50m, 10.00m),
        ["gpt-5-mini"] = new(0.50m, 2.00m),

        // GPT-4.1 family.
        ["gpt-4.1"] = new(2.00m, 8.00m),
        ["gpt-4.1-mini"] = new(0.40m, 1.60m),

        // GPT-4o family.
        ["gpt-4o"] = new(2.50m, 10.00m),
        ["gpt-4o-mini"] = new(0.15m, 0.60m)
    };

    public static Cost Compute(string model, string? deployment, int inputTokens, int outputTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (!Models.TryGetValue(model, out var pricing))
        {
            // Unknown model: return zero-cost so the runtime stays usable for
            // ad-hoc dev deployments. Phase 5's router can refuse to route
            // to a model with no pricing entry.
            return new Cost(model, deployment, inputTokens, outputTokens, 0m);
        }
        var input = pricing.InputPerMillion * inputTokens / 1_000_000m;
        var output = pricing.OutputPerMillion * outputTokens / 1_000_000m;
        return new Cost(model, deployment, inputTokens, outputTokens, decimal.Round(input + output, 6));
    }
}
