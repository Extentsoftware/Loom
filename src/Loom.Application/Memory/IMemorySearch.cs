using Loom.Domain.Nodes;

namespace Loom.Application.Memory;

/// <summary>
/// Cross-project similarity / search seam. Phase-6a ships a SQL-backed
/// implementation that wraps the existing FeatureNode keyword search;
/// Phase-6b swaps in an Elasticsearch hybrid (BM25 + vector) implementation
/// against the same interface. The memory-lookup workflow step calls this
/// to produce the "we've seen something like this before" panel without
/// caring about the backend.
/// </summary>
public interface IMemorySearch
{
    /// <summary>
    /// Find up to <paramref name="limit"/> nodes similar to the query text,
    /// optionally scoped to a project. Results are ranked by relevance —
    /// the SQL implementation uses a simple recency + match heuristic; the
    /// ES implementation will use BM25 + vector.
    /// </summary>
    Task<SimilarityReport> SearchSimilarAsync(
        string query,
        Guid? projectId,
        int limit,
        CancellationToken ct = default);
}

/// <summary>
/// Phase-6a similarity output: a list of candidates with a score and a
/// one-sentence summary derived from the node's intent. The summary is
/// pre-computed (no agent call) so the panel renders fast.
/// </summary>
public sealed record SimilarityReport(
    string Query,
    IReadOnlyList<SimilarityCandidate> Candidates);

public sealed record SimilarityCandidate(
    NodeId NodeId,
    Guid ProjectId,
    string Title,
    string? Summary,
    double Score);
