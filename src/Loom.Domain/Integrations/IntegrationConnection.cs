using Loom.Domain.Common;

namespace Loom.Domain.Integrations;

public readonly record struct IntegrationConnectionId(Guid Value) : IEntityId
{
    public static IntegrationConnectionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// External system Loom is wired to (ADO, Atlassian, Figma, Miro, Graph,
/// GitHub, Confluence, …). The kind enum is closed; new integrations get
/// new values, never repurpose existing ones — webhook payloads in flight
/// reference these by id.
/// </summary>
public enum IntegrationKind
{
    AzureDevOps = 1,
    Atlassian = 2,
    Figma = 3,
    Miro = 4,
    GraphTeams = 5,
    GitHub = 6,
    Confluence = 7
}

public enum IntegrationStatus
{
    Active = 1,
    Disabled = 2,
    /// <summary>Last health check failed; webhook deliveries may be missing.</summary>
    Degraded = 3
}

/// <summary>
/// One configured external integration. The actual credential — PAT, OAuth
/// access token, app secret — never lives in this aggregate; it is stored
/// separately in Key Vault and looked up via the <c>SecretRef</c>. The
/// shared HMAC secret used to validate inbound webhooks does live here
/// because validation needs to be synchronous in the HTTP pipeline.
/// </summary>
public sealed class IntegrationConnection
{
    private IntegrationConnection() { } // EF Core

    private IntegrationConnection(
        IntegrationConnectionId id,
        Guid? projectId,
        IntegrationKind kind,
        string displayName,
        string tenantOrAccount,
        string? secretRef,
        string? webhookSecret,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        Kind = kind;
        DisplayName = displayName;
        TenantOrAccount = tenantOrAccount;
        SecretRef = secretRef;
        WebhookSecret = webhookSecret;
        Status = IntegrationStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public IntegrationConnectionId Id { get; private set; }

    /// <summary>
    /// Project this connection is scoped to. Null = global / cross-project
    /// (e.g. an org-wide ADO app registration). Per-project connections win
    /// over global when both apply.
    /// </summary>
    public Guid? ProjectId { get; private set; }

    public IntegrationKind Kind { get; private set; }
    public string DisplayName { get; private set; } = null!;

    /// <summary>
    /// Identifier in the external system: ADO organisation name, Atlassian
    /// site URL, Figma team id, etc. Used by the adapter to know what to
    /// hit; not a credential.
    /// </summary>
    public string TenantOrAccount { get; private set; } = null!;

    /// <summary>Key Vault secret URI for the access credential. Null until configured.</summary>
    public string? SecretRef { get; private set; }

    /// <summary>HMAC shared secret for inbound webhook validation.</summary>
    public string? WebhookSecret { get; private set; }

    public IntegrationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastHealthCheckAt { get; private set; }

    public static IntegrationConnection Create(
        Guid? projectId,
        IntegrationKind kind,
        string displayName,
        string tenantOrAccount,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantOrAccount);
        return new IntegrationConnection(
            IntegrationConnectionId.New(),
            projectId,
            kind,
            displayName.Trim(),
            tenantOrAccount.Trim(),
            secretRef: null,
            webhookSecret: null,
            createdAt: now);
    }

    public void SetSecretRef(string? secretRef, DateTimeOffset now)
    {
        SecretRef = string.IsNullOrWhiteSpace(secretRef) ? null : secretRef.Trim();
        UpdatedAt = now;
    }

    public void SetWebhookSecret(string? webhookSecret, DateTimeOffset now)
    {
        WebhookSecret = string.IsNullOrWhiteSpace(webhookSecret) ? null : webhookSecret.Trim();
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        Status = IntegrationStatus.Disabled;
        UpdatedAt = now;
    }

    public void Enable(DateTimeOffset now)
    {
        Status = IntegrationStatus.Active;
        UpdatedAt = now;
    }

    public void RecordHealthCheck(bool ok, DateTimeOffset now)
    {
        LastHealthCheckAt = now;
        Status = ok ? IntegrationStatus.Active : IntegrationStatus.Degraded;
        UpdatedAt = now;
    }
}
