using Loom.Application.Abstractions;
using Loom.Domain.Nodes;

namespace Loom.Application.Memory;

/// <summary>
/// Phase-6a implementation: delegates to FeatureNode keyword search and
/// scores by token overlap + recency. Honest about its limits — the ADR
/// notes that BM25/vector via Elasticsearch is the Phase-6b upgrade and
/// this implementation should be replaced wholesale, not extended.
/// </summary>
public sealed class SqlMemorySearch(
    IFeatureNodeRepository nodes,
    ISystemClock clock) : IMemorySearch
{
    public async Task<SimilarityReport> SearchSimilarAsync(
        string query,
        Guid? projectId,
        int limit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SimilarityReport(query ?? string.Empty, []);
        }

        var queryTokens = Tokenize(query);
        // The repository's SearchAsync is a substring match; multi-word
        // queries rarely appear verbatim. Submit the first significant
        // token to widen the candidate set, then score against ALL query
        // tokens for ranking. The ES-backed Phase-6b implementation will
        // replace this two-step dance with a real BM25 query.
        var probe = queryTokens.FirstOrDefault() ?? query;
        var candidates = await nodes.SearchAsync(probe, projectId, take: Math.Max(limit * 4, 20), ct);
        var now = clock.UtcNow;

        var scored = candidates
            .Select(n => new SimilarityCandidate(
                NodeId: n.Id,
                ProjectId: n.ProjectId,
                Title: n.Title,
                Summary: SummarizeIntent(n.Intent),
                Score: ScoreNode(n, queryTokens, now)))
            .Where(c => c.Score > 0)
            .OrderByDescending(c => c.Score)
            .Take(limit)
            .ToList();

        return new SimilarityReport(query, scored);
    }

    private static double ScoreNode(FeatureNode node, IReadOnlySet<string> queryTokens, DateTimeOffset now)
    {
        var title = Tokenize(node.Title);
        var intent = Tokenize(node.Intent ?? string.Empty);

        var titleHits = title.Intersect(queryTokens).Count();
        var intentHits = intent.Intersect(queryTokens).Count();

        var match = (titleHits * 2.0) + intentHits;
        if (match <= 0)
        {
            return 0;
        }

        // Recency boost: linear decay from 1.0 (today) to 0.5 over 90 days.
        var ageDays = Math.Max(0, (now - node.UpdatedAt).TotalDays);
        var recency = Math.Max(0.5, 1.0 - (ageDays / 180.0));
        return match * recency;
    }

    private static string? SummarizeIntent(string? intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            return null;
        }
        var trimmed = intent.Trim();
        // First sentence, capped at 200 chars.
        var dot = trimmed.IndexOf('.', StringComparison.Ordinal);
        var first = dot > 20 ? trimmed[..(dot + 1)] : trimmed;
        return first.Length > 200 ? string.Concat(first.AsSpan(0, 197), "...") : first;
    }

    private static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        return text
            .Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 3)
            .Select(t => t.ToLowerInvariant())
            .Where(t => !Stopwords.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly char[] _separators =
    [
        ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}',
        '"', '\'', '-', '/', '\\', '|'
    ];

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "from", "are", "was", "were", "have",
        "has", "had", "but", "not", "can", "will", "would", "should", "could", "into",
        "their", "there", "they", "them", "what", "when", "which", "while", "your", "you"
    };
}
