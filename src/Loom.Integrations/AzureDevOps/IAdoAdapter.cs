namespace Loom.Integrations.AzureDevOps;

/// <summary>
/// Phase-2 read-only ADO adapter. The Phase-2 plan is deliberately narrow:
/// list a project's repos, fetch metadata for one, parse a webhook payload.
/// Phase 5+ extends this with work-item read/write + pipelines.
///
/// Auth: Entra app + on-behalf-of from day one (per the Phase-2 ADR);
/// per-user PAT is the local-dev fallback. The adapter receives a token
/// resolved upstream — it does not negotiate auth itself.
/// </summary>
public interface IAdoAdapter
{
    Task<IReadOnlyList<AdoRepoSummary>> ListReposAsync(string organisation, string project, string accessToken, CancellationToken ct = default);
    Task<AdoRepoSummary?> GetRepoAsync(string organisation, string project, string repoName, string accessToken, CancellationToken ct = default);
}

public sealed record AdoRepoSummary(
    string Id,
    string Name,
    string DefaultBranch,
    Uri RemoteUrl,
    Uri WebUrl);
