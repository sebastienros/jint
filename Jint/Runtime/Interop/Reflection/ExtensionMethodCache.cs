using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Jint.Extensions;

#pragma warning disable IL2067
#pragma warning disable IL2070

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// An extension method lookup built from the extension methods provided via options. It is shared between
/// engines rather than merely shareable: <see cref="Build"/> interns one instance per distinct ordered
/// container type list, so engines configured alike hold the very same lookup.
/// </summary>
internal sealed class ExtensionMethodCache
{
    internal static readonly ExtensionMethodCache Empty = new(new Dictionary<Type, MethodInfo[]>());

    // Process-wide memo of built lookups, keyed on the ordered container type list. Two engines registering
    // the same containers then get the *same instance*, which is what the interop accessor cache needs: it is
    // partitioned by InteropResolutionProfile, which compares this lookup by reference, so a fresh instance
    // per engine would give every extension method host its own never-matchable partition of a cache that
    // never evicts. Interning also means the reflection sweep below runs once per distinct configuration
    // rather than once per engine, and the derived per-queried-type lookup stays warm across engines.
    //
    // Everything an entry holds derives from the container types alone - a Type -> MethodInfo[] map and the
    // per-queried-type lookup derived from it - so nothing engine-affine is interned. The retention is the
    // container types and their methods for the lifetime of the process, bounded by the number of distinct
    // registration lists, and matches what the accessor cache this feeds already commits to.
    private static readonly ConcurrentDictionary<ContainerTypeListKey, ExtensionMethodCache> _builtCaches = new();

    // starting point containing only extension methods targeting one type, based on given options configuration
    private readonly Dictionary<Type, MethodInfo[]> _allExtensionMethods;

    // cache of all possibilities for type including base types and implemented interfaces
    private Dictionary<Type, MethodInfo[]> _extensionMethods = new();

    private ExtensionMethodCache(Dictionary<Type, MethodInfo[]> extensionMethods)
    {
        _allExtensionMethods = extensionMethods;
    }

    internal static ExtensionMethodCache Build(List<Type> extensionMethodContainerTypes)
    {
        if (extensionMethodContainerTypes.Count == 0)
        {
            return Empty;
        }

        var key = new ContainerTypeListKey(extensionMethodContainerTypes.ToArray());
        return _builtCaches.GetOrAdd(key, static k => BuildCore(k.Types));
    }

    private static ExtensionMethodCache BuildCore(Type[] extensionMethodContainerTypes)
    {
        static Type GetTypeDefinition(Type type)
        {
            return type.IsConstructedGenericType && type.GenericTypeArguments.Any(x => x.IsGenericParameter) ? type.GetGenericTypeDefinition() : type;
        }

        var methodsByTarget = extensionMethodContainerTypes
            .SelectMany(x => x.GetExtensionMethods())
            .GroupBy(x => GetTypeDefinition(x.GetParameters()[0].ParameterType))
            .ToDictionary(x => x.Key, x => x.ToArray());

        return new ExtensionMethodCache(methodsByTarget);
    }

    /// <summary>
    /// A snapshot of the registered container type list, compared element-wise and in order.
    /// </summary>
    /// <remarks>
    /// Order is part of the identity on purpose: registration order decides the order of the
    /// <see cref="MethodInfo"/> arrays below, and therefore the order overload candidates are considered in.
    /// <c>[A, B]</c> and <c>[B, A]</c> consequently intern separately, which keeps behaviour exactly as it
    /// was when every engine built its own lookup.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    private readonly struct ContainerTypeListKey : IEquatable<ContainerTypeListKey>
    {
        private readonly int _hashCode;

        internal ContainerTypeListKey(Type[] types)
        {
            Types = types;

            var hashCode = types.Length;
            foreach (var type in types)
            {
                hashCode = (hashCode * 397) ^ type.GetHashCode();
            }

            _hashCode = hashCode;
        }

        internal Type[] Types { get; }

        public bool Equals(ContainerTypeListKey other)
        {
            if (_hashCode != other._hashCode)
            {
                return false;
            }

            var types = Types;
            var otherTypes = other.Types;
            if (types.Length != otherTypes.Length)
            {
                return false;
            }

            for (var i = 0; i < types.Length; i++)
            {
                if (types[i] != otherTypes[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ContainerTypeListKey other && Equals(other);

        public override int GetHashCode() => _hashCode;
    }

    public bool HasMethods => _allExtensionMethods.Count > 0;

    public bool TryGetExtensionMethods(Type objectType, [NotNullWhen(true)] out MethodInfo[]? methods)
    {
        if (_allExtensionMethods.Count == 0)
        {
            methods = [];
            return false;
        }

        var methodLookup = _extensionMethods;

        if (methodLookup.TryGetValue(objectType, out methods))
        {
            return methods.Length > 0;
        }

        var results = new List<MethodInfo>();
        if (_allExtensionMethods.TryGetValue(objectType, out var ownExtensions))
        {
            results.AddRange(ownExtensions);
        }

        foreach (var parentType in GetParentTypes(objectType))
        {
            if (_allExtensionMethods.TryGetValue(parentType, out var parentExtensions))
            {
                results.AddRange(parentExtensions);
            }
        }

        // don't create generic methods bound to an array of object - as this will prevent value types and other generics that don't support covariants/contravariants
        methods = results.ToArray();

        // racy, we don't care, worst case we'll catch up later
        Interlocked.CompareExchange(ref _extensionMethods, new Dictionary<Type, MethodInfo[]>(methodLookup) { [objectType] = methods }, methodLookup);

        return methods.Length > 0;
    }

    private static IEnumerable<Type> GetParentTypes(Type type)
    {
        // is there any base type?
        if (type == null)
        {
            yield break;
        }

        // return all implemented or inherited interfaces
        foreach (var i in type.GetInterfaces())
        {
            yield return i;

            if (i.IsConstructedGenericType)
            {
                yield return i.GetGenericTypeDefinition();
            }
        }

        // return all inherited types
        var currentBaseType = type.BaseType;
        while (currentBaseType != null)
        {
            yield return currentBaseType;

            if (currentBaseType.IsConstructedGenericType)
            {
                yield return currentBaseType.GetGenericTypeDefinition();
            }

            currentBaseType = currentBaseType.BaseType;
        }
    }
}
