using System.Text.Json;
using Loom.Application.Workflows;

namespace Loom.Application.Memory;

/// <summary>
/// Phase-6a in-proc workflow step that runs a memory search against the
/// step's input text and emits a JSON report of similar prior nodes for
/// downstream agent steps to fold into context. Resolved by step key
/// "memory-lookup".
///
/// Inputs (the WorkflowEngine maps step inputs into this dictionary):
///   "memory-lookup.query"      — required, free text to search.
///   "memory-lookup.project_id" — optional, scopes search to a project.
///   "memory-lookup.limit"      — optional, max candidates (default 5).
///
/// Output: serialized SimilarityReport JSON.
/// </summary>
public sealed class MemoryLookupStep(IMemorySearch search) : IInProcStep
{
    public const string Key = "memory-lookup";
    public string StepKey => Key;

    public async Task<string> ExecuteAsync(IReadOnlyDictionary<string, string> inputs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var query = TryGet(inputs, "memory-lookup.query");
        if (string.IsNullOrWhiteSpace(query))
        {
            // Step is harmless when no query is provided — emit an empty
            // report rather than failing the workflow. Agent steps that
            // expect candidates should check the array length.
            return JsonSerializer.Serialize(new SimilarityReport(string.Empty, []));
        }

        Guid? projectId = null;
        if (TryGet(inputs, "memory-lookup.project_id") is { Length: > 0 } pid &&
            Guid.TryParse(pid, out var parsed))
        {
            projectId = parsed;
        }

        var limit = 5;
        if (TryGet(inputs, "memory-lookup.limit") is { Length: > 0 } limStr &&
            int.TryParse(limStr, System.Globalization.CultureInfo.InvariantCulture, out var parsedLimit) &&
            parsedLimit > 0)
        {
            limit = parsedLimit;
        }

        var report = await search.SearchSimilarAsync(query, projectId, limit, ct);
        return JsonSerializer.Serialize(report);
    }

    private static string? TryGet(IReadOnlyDictionary<string, string> inputs, string key) =>
        inputs.TryGetValue(key, out var v) ? v : null;
}
