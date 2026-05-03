using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loom.Integrations.AzureDevOps;

/// <summary>
/// HttpClient-based read-only ADO adapter targeting the v7.1 REST API. The
/// adapter accepts a bearer access token per call rather than holding a
/// long-lived credential — Loom's auth/permissions layer mints tokens
/// on-behalf-of the requesting user.
/// </summary>
public sealed class AdoAdapter(HttpClient httpClient) : IAdoAdapter
{
    private const string ApiVersion = "api-version=7.1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AdoRepoSummary>> ListReposAsync(
        string organisation, string project, string accessToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisation);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"https://dev.azure.com/{organisation}/{project}/_apis/git/repositories?{ApiVersion}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        var page = JsonSerializer.Deserialize<RepoListPage>(body, Json);
        if (page is null)
        {
            return [];
        }
        return [.. page.Value.Select(MapRepo)];
    }

    public async Task<AdoRepoSummary?> GetRepoAsync(
        string organisation, string project, string repoName, string accessToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisation);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoName);

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"https://dev.azure.com/{organisation}/{project}/_apis/git/repositories/{Uri.EscapeDataString(repoName)}?{ApiVersion}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        var dto = JsonSerializer.Deserialize<RepoDto>(body, Json);
        return dto is null ? null : MapRepo(dto);
    }

    private static AdoRepoSummary MapRepo(RepoDto r) => new(
        Id: r.Id ?? string.Empty,
        Name: r.Name ?? string.Empty,
        DefaultBranch: TrimBranch(r.DefaultBranch),
        RemoteUrl: new Uri(r.RemoteUrl ?? "https://dev.azure.com/"),
        WebUrl: new Uri(r.WebUrl ?? "https://dev.azure.com/"));

    private static string TrimBranch(string? raw) =>
        string.IsNullOrEmpty(raw) ? "main" : raw.Replace("refs/heads/", "", StringComparison.Ordinal);

    private sealed class RepoListPage
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("value")] public List<RepoDto> Value { get; set; } = [];
    }

    private sealed class RepoDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("defaultBranch")] public string? DefaultBranch { get; set; }
        [JsonPropertyName("remoteUrl")] public string? RemoteUrl { get; set; }
        [JsonPropertyName("webUrl")] public string? WebUrl { get; set; }
    }
}
