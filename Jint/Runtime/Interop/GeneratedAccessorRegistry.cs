using System.Diagnostics.CodeAnalysis;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop;

/// <summary>
/// Static registry of accessors emitted by the [JsAccessible] source generator. Each
/// generator-emitted partial calls <see cref="Register"/> from a <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>
/// method during assembly load, so by the time JS code touches a wrapped CLR object the registry
/// already holds direct typed accessors for every annotated member.
/// </summary>
public static class GeneratedAccessorRegistry
{
    // Lookups happen on every CLR property/method access — keep the path lock-free. The dictionary
    // is built up at module-init time (sequential, before user code can touch it) and effectively
    // read-only thereafter, so a regular Dictionary suffices once we publish the assembled inner
    // map per type with a single Volatile.Write.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Dictionary<string, ReflectionAccessor>> _byType
        = new();

    /// <summary>
    /// Registers a generated accessor for <paramref name="type"/>'s <paramref name="memberName"/>.
    /// Called from the generator's [ModuleInitializer]; not intended for hand-written user code.
    /// </summary>
    public static void Register(Type type, string memberName, ReflectionAccessor accessor)
    {
        var members = _byType.GetOrAdd(type, static _ => new Dictionary<string, ReflectionAccessor>(StringComparer.Ordinal));
        members[memberName] = accessor;
    }

    internal static bool TryGet(Type type, string memberName, [NotNullWhen(true)] out ReflectionAccessor? accessor)
    {
        if (_byType.TryGetValue(type, out var members) && members.TryGetValue(memberName, out accessor))
        {
            return true;
        }

        accessor = null;
        return false;
    }
}
