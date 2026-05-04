using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Loom.Infrastructure;
using Loom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Loom seed tool — creates a project and a small node tree so the
// Operating Picture has something real to render in development.
//
// Usage:
//   dotnet run --project tools/seed
//
// Connection string lookup order:
//   - LOOM_CONNECTION env var (matches Loom.Web)
//   - default LocalDB
//
// Idempotent: if the project slug already exists, nothing is changed.

// Default points at the docker-compose MSSQL service (`docker compose up`).
// Override with LOOM_CONNECTION when running against another DB.
var connection =
    Environment.GetEnvironmentVariable("LOOM_CONNECTION")
    ?? "Server=localhost,1433;Database=loom;User Id=sa;Password=Loom_dev_pwd_1!;TrustServerCertificate=True;Encrypt=False";

Console.WriteLine($"[seed] connection: {Redact(connection)}");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddLoomInfrastructure(connection);
    })
    .Build();

using var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;

// Apply migrations so the seed tool also works on a fresh database.
var ctx = sp.GetRequiredService<LoomDbContext>();
Console.WriteLine("[seed] applying migrations…");
await ctx.Database.MigrateAsync();

var clock = sp.GetRequiredService<ISystemClock>();
var projects = sp.GetRequiredService<IProjectRepository>();
var nodes = sp.GetRequiredService<IFeatureNodeRepository>();
var uow = sp.GetRequiredService<IUnitOfWork>();

const string projectSlug = "retail-web";
var existing = await projects.GetBySlugAsync(projectSlug);
if (existing is not null)
{
    Console.WriteLine($"[seed] project '{projectSlug}' already exists; nothing to do.");
    return;
}

Console.WriteLine($"[seed] creating project '{projectSlug}'…");

var project = Project.Create(Slug.From(projectSlug), "Retail Web", clock.UtcNow);
project.UpdateDescription("Public retail storefront and checkout.", clock.UtcNow);
await projects.AddAsync(project);

// Seed owner — a placeholder GUID until real user accounts exist.
var ownerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

var checkout = FeatureNode.Create(
    project.Id, parentId: null,
    Slug.From("checkout-2026"),
    NodeType.Initiative,
    "Checkout 2026",
    ownerId,
    clock.UtcNow);
checkout.SetIntent(
    "Lift completion on cart-to-confirmation by removing friction for returning users " +
    "and clarifying the path for guests, without compromising PCI scope.",
    clock.UtcNow);
await nodes.AddAsync(checkout);

var express = FeatureNode.Create(
    project.Id, parentId: checkout.Id,
    Slug.From("express-checkout"),
    NodeType.Feature,
    "Express checkout flow",
    ownerId,
    clock.UtcNow);
express.SetIntent(
    "Returning users with saved cards complete checkout in fewer than three clicks.",
    clock.UtcNow);
express.AdvancePhase(NodePhase.Enrich, clock.UtcNow);
await nodes.AddAsync(express);

var savedCard = FeatureNode.Create(
    project.Id, parentId: express.Id,
    Slug.From("saved-card-surfacing"),
    NodeType.Capability,
    "Saved card surfacing on cart",
    ownerId,
    clock.UtcNow);
savedCard.SetIntent(
    "Default-highlight a tokenized card on cart for returning users.",
    clock.UtcNow);
savedCard.AdvancePhase(NodePhase.Enrich, clock.UtcNow);
await nodes.AddAsync(savedCard);

var cvv = FeatureNode.Create(
    project.Id, parentId: express.Id,
    Slug.From("cvv-revalidation"),
    NodeType.Capability,
    "CVV revalidation step",
    ownerId,
    clock.UtcNow);
cvv.SetIntent("Conditional CVV input for issuers that require it.", clock.UtcNow);
await nodes.AddAsync(cvv);

var search = FeatureNode.Create(
    project.Id, parentId: null,
    Slug.From("search-relevance"),
    NodeType.Initiative,
    "Search relevance overhaul",
    ownerId,
    clock.UtcNow);
search.SetIntent(
    "Improve product discovery for long-tail queries by re-indexing with synonyms " +
    "and re-ranking by behavioural signals from MSSQL telemetry.",
    clock.UtcNow);
await nodes.AddAsync(search);

await uow.SaveChangesAsync();

Console.WriteLine("[seed] done.");
Console.WriteLine($"[seed]  · project: {project.Slug} ({project.Id})");
Console.WriteLine("[seed]  · 5 nodes created");

return;

static string Redact(string conn)
{
    // crude redaction of password/pwd in the connection string for logs
    var parts = conn.Split(';');
    return string.Join(';', parts.Select(p =>
    {
        var i = p.IndexOf('=');
        if (i <= 0) return p;
        var key = p[..i].Trim();
        return key.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("Pwd", StringComparison.OrdinalIgnoreCase)
            ? $"{key}=***"
            : p;
    }));
}
