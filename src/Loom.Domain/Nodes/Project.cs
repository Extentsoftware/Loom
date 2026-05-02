using Loom.Domain.Common;

namespace Loom.Domain.Nodes;

/// <summary>
/// A project owns a tree of nodes and a body of project-scoped fragments.
/// In v1 a deployment hosts one or two projects; multi-tenancy comes later.
/// </summary>
public sealed class Project
{
    private Project() { }

    private Project(Guid id, Slug slug, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Slug = slug;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Slug Slug { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Project Create(Slug slug, string name, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Project(Guid.CreateVersion7(), slug, name.Trim(), now);
    }

    public void UpdateDescription(string? description, DateTimeOffset now)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = now;
    }

    public void Rename(string name, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        UpdatedAt = now;
    }
}
