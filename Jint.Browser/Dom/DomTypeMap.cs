using System.Collections.Concurrent;

namespace Jint.Browser.Dom;

/// <summary>
/// Maps a native DOM runtime type to the WebIDL interface whose prototype its wrappers use.
/// </summary>
/// <remarks>
/// The generated contract supplies the mappings; unknown element tags retain the appropriate HTML or SVG
/// fallback interface instead of creating a second type hierarchy in the binding layer.
/// </remarks>
internal static partial class DomTypeMap
{
    private static readonly ConcurrentDictionary<Type, DomInterfaceDefinition?> _cache = new();

    /// <summary>
    /// The interface <paramref name="type"/>'s instances are wrapped as, or <see langword="null"/> when the
    /// type implements no generated interface at all.
    /// </summary>
    internal static DomInterfaceDefinition? For(Type type) => _cache.GetOrAdd(type, static t =>
    {
        foreach (var candidate in _candidates)
        {
            if (candidate.ClrInterface.IsAssignableFrom(t))
            {
                return candidate;
            }
        }

        return null;
    });
}
