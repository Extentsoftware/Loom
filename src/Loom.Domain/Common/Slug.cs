using System.Text.RegularExpressions;

namespace Loom.Domain.Common;

/// <summary>
/// Human-readable, URL-safe identifier for nodes, fragments, projects.
/// Lowercase letters, numbers, hyphens. Cannot start or end with a hyphen.
/// Max 80 chars to leave room in URN composition.
/// </summary>
public readonly partial record struct Slug
{
    private const int MaxLength = 80;

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug From(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"Slug exceeds {MaxLength} characters.", nameof(raw));
        }
        if (!SlugPattern().IsMatch(trimmed))
        {
            throw new ArgumentException(
                $"'{trimmed}' is not a valid slug. Use lowercase letters, numbers, and hyphens only.",
                nameof(raw));
        }
        return new Slug(trimmed);
    }

    public static bool TryFrom(string raw, out Slug slug)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaxLength || !SlugPattern().IsMatch(raw))
        {
            slug = default;
            return false;
        }
        slug = new Slug(raw);
        return true;
    }

    public override string ToString() => Value;

    public static implicit operator string(Slug s) => s.Value;
}
