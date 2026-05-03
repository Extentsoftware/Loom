using Loom.Domain.Common;
using Loom.Domain.Fragments;

namespace Loom.Domain.Workflows;

/// <summary>
/// Selects a fragment to compose into a step's prompt by category and key.
/// The composer applies the selector against the effective fragment set
/// (global → project → ancestor chain → node) and picks the
/// most-specific scope's match.
/// </summary>
public sealed record FragmentSelector(FragmentCategory Category, Slug Key);
