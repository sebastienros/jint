using System.Collections.Concurrent;

namespace Jint.Runtime.Interop;

/// <summary>
/// Answers "did the host promise that instances of this CLR type do not change while they are exposed to this
/// engine?" — the promise made by <see cref="OptionsExtensions.AddImmutableCrossing"/>, which is what lets an
/// <see cref="ObjectWrapper"/> memoize what it reads through such a target instead of re-reading the CLR
/// member or dictionary key on every access.
/// <para>
/// The rule is plain assignability against the declared types, and deliberately <b>not</b> the two-way
/// "could some type be both" reasoning <see cref="ObjectConverterTypeFilter"/> uses. The two filters err in
/// opposite directions: there, a wrong <c>true</c> only forgoes a fast lane, so guessing <c>true</c> is the
/// safe answer; here a wrong <c>true</c> would memoize a mutable object and serve stale reads, so only a type
/// the host actually declared — directly, through a base class, or through an interface — is claimed.
/// </para>
/// <para>
/// The per-type answer is memoized: a host exposes a small, fixed set of types, so the steady-state cost of a
/// crossing is one dictionary probe rather than a walk over the declared list.
/// </para>
/// </summary>
internal sealed class ImmutableCrossingTypeFilter
{
    private readonly Type[] _declaredTypes;
    private readonly ConcurrentDictionary<Type, bool> _claims = new();
    private Func<Type, bool>? _computeClaims;

    private ImmutableCrossingTypeFilter(Type[] declaredTypes)
    {
        _declaredTypes = declaredTypes;
    }

    /// <summary>
    /// Builds the filter for an engine's declared types, or <see langword="null"/> when the host declared
    /// none — in which case no wrapper ever asks and the whole mechanism costs a single null check.
    /// </summary>
    internal static ImmutableCrossingTypeFilter? Create(List<Type> declaredTypes)
    {
        if (declaredTypes.Count == 0)
        {
            return null;
        }

        return new ImmutableCrossingTypeFilter(declaredTypes.ToArray());
    }

    /// <summary>
    /// Whether an instance of <paramref name="targetType"/> is covered by the host's immutability promise.
    /// </summary>
    internal bool Claims(Type targetType)
    {
        // cannot be static: the answer depends on _declaredTypes, and the state-carrying GetOrAdd overload
        // does not exist on every target framework, so the delegate is built once per filter
        return _claims.GetOrAdd(targetType, _computeClaims ??= type => ComputeClaims(_declaredTypes, type));
    }

    private static bool ComputeClaims(Type[] declaredTypes, Type targetType)
    {
        foreach (var declaredType in declaredTypes)
        {
            // an open generic (typeof(IDictionary<,>)) is not comparable through IsAssignableFrom at all,
            // and unlike the converter filter this one may not guess in the permissive direction
            if (declaredType.ContainsGenericParameters)
            {
                continue;
            }

            if (declaredType.IsAssignableFrom(targetType))
            {
                return true;
            }
        }

        return false;
    }
}
