using System.Reflection;
using Loom.Domain.Common;

namespace Loom.Infrastructure.Outbox;

/// <summary>
/// Maps the EventType column (full type name) back to a runtime Type, so the
/// dispatcher can deserialize JSON payloads into the right concrete record.
/// Built once at startup by scanning the supplied assemblies for non-abstract
/// IDomainEvent implementations.
/// </summary>
public sealed class DomainEventTypeRegistry
{
    private readonly Dictionary<string, Type> _byFullName;

    public DomainEventTypeRegistry(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        _byFullName = assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t is not null)!;
                }
            })
            .Where(t => t is not null
                && t.IsClass
                && !t.IsAbstract
                && typeof(IDomainEvent).IsAssignableFrom(t)
                && t.FullName is not null)
            .ToDictionary(t => t!.FullName!, t => t!, StringComparer.Ordinal);
    }

    public bool TryResolve(string eventType, out Type type) =>
        _byFullName.TryGetValue(eventType, out type!);

    public IReadOnlyCollection<Type> KnownTypes => _byFullName.Values;
}
