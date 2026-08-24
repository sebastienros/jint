using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Native.Array;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// The eight hooks a host implements to project <em>named</em> properties, as the shared implementation in
/// <see cref="NamedProjection"/> sees them. Both <see cref="NamedPropertyObject"/> — where the projection is
/// the whole object — and <see cref="ArrayLikeObject"/> — where it is the named half beside the indexed one —
/// publish exactly these members to their subclasses, which is what makes the two derive the <em>same</em>
/// model from the same declarations.
/// </summary>
/// <remarks>
/// It is <see langword="internal"/> and implemented explicitly by both classes, so it appears in no public
/// signature and a host can neither implement it nor call it: a host reaches these through the
/// <c>protected</c>/<c>public</c> members on whichever base class it derived from.
/// </remarks>
internal interface INamedProjection
{
    int NameCount { get; }
    string NameAt(int index);
    bool TryGetNamedValue(string name, out JsValue value);
    bool HasName(string name);
    bool IsNameEnumerable(string name);
    bool IsNameWritable(string name);
    bool TrySetNamedValue(string name, JsValue value);
    bool TryDeleteName(string name);
}

/// <summary>
/// Which of the optional named hooks a host type actually declared. Derived from the runtime type once and
/// cached process-wide, exactly the way <c>ObjectInstance</c> derives <c>PropertyAccessSemantics</c> and its
/// <c>ProbeOwnProperty</c> override flag: the answer depends only on the type, and an entry retains nothing
/// but its <see cref="Type"/> key.
/// </summary>
[Flags]
internal enum NamedProjectionHooks
{
    None = 0,

    /// <summary>The type declares a named projection at all — any of the eight hooks is overridden.</summary>
    Any = 1,

    /// <summary><c>IsNameWritable</c> is overridden, so some name may report <c>writable: true</c>.</summary>
    Writable = 2,

    /// <summary><c>TrySetNamedValue</c> is overridden, so a write can be accepted.</summary>
    Write = 4,
}

/// <summary>
/// The named-property half of the host-object model, written once and used by both
/// <see cref="NamedPropertyObject"/> and <see cref="ArrayLikeObject"/>.
/// </summary>
/// <remarks>
/// <para>
/// The point of the sharing is not code size. The whole promise of these two base classes is that a host
/// declares its data and the class derives a <em>coherent</em> property model from it — <c>GetOwnProperty</c>,
/// <c>TryGetOwnPropertyValue</c>, <c>ProbeOwnProperty</c>, both key enumerations, <c>Set</c>, <c>Delete</c> and
/// <c>DefineOwnProperty</c> all agreeing. Two hand-kept copies of that derivation would reproduce, inside the
/// engine, exactly the incoherence the classes exist to spare a host.
/// </para>
/// <para>
/// Every method here takes the projection as an <see cref="INamedProjection"/>, so the calls into the host are
/// interface dispatch — the same single indirection a <c>protected virtual</c> call would have been.
/// </para>
/// </remarks>
internal static class NamedProjection
{
    /// <summary><c>{ writable: false, enumerable: true, configurable: true }</c>.</summary>
    private const PropertyFlag ReadOnlyEnumerable = PropertyFlag.NonWritable;

    /// <summary><c>{ writable: false, enumerable: false, configurable: true }</c>.</summary>
    private const PropertyFlag ReadOnlyNonEnumerable = PropertyFlag.OnlyConfigurable;

    /// <summary><c>{ writable: true, enumerable: true, configurable: true }</c>.</summary>
    private const PropertyFlag WritableEnumerable = PropertyFlag.ConfigurableEnumerableWritable;

    /// <summary><c>{ writable: true, enumerable: false, configurable: true }</c>.</summary>
    private const PropertyFlag WritableNonEnumerable = PropertyFlag.NonEnumerable;

    /// <summary>
    /// How <see cref="CollectNames"/> orders what <c>NameAt</c> reports.
    /// </summary>
    internal enum NameOrder
    {
        /// <summary>
        /// Ordinary <c>[[OwnPropertyKeys]]</c> order for an object whose keys are all named: the projected
        /// names that are canonical array indices first and ascending, then the rest in <c>NameAt</c> order.
        /// </summary>
        IndexNamesFirst,

        /// <summary>
        /// <c>NameAt</c> order verbatim, for a projection that sits beside an indexed one: a canonical array
        /// index and <c>length</c> belong to the collection there, so a projected name spelling one of them is
        /// dropped rather than advertised for a key the read path would never route to it.
        /// </summary>
        BesideIndices,
    }

    /// <summary>
    /// The key as a name, or <see langword="false"/> for a key a named projection cannot own — a symbol, a
    /// private name, or an object key that would need an observable <c>ToPrimitive</c>. Those fall through to
    /// the ordinary property bag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetName(JsValue property, [NotNullWhen(true)] out string? name)
    {
        if (property is JsString jsString)
        {
            name = jsString.ToString();
            return true;
        }

        if (property is JsSymbol or PrivateName or ObjectInstance)
        {
            name = null;
            return false;
        }

        name = TypeConverter.ToString(property);
        return true;
    }

    /// <summary>
    /// The single funnel every engine-side named read goes through, so the host contract is enforced in one
    /// place: a <see langword="false"/> answer always leaves <paramref name="value"/> as <c>undefined</c>
    /// rather than whatever the host left in the <c>out</c> slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Read(INamedProjection projection, string name, out JsValue value)
    {
        if (projection.TryGetNamedValue(name, out value))
        {
            if (HostContractVerification.Enabled && value is null)
            {
                HostContractVerification.Fail($"{projection.GetType()}.TryGetNamedValue answered '{name}' with a CLR null; return a JsValue or answer false.");
            }

            return true;
        }

        value = JsValue.Undefined;
        return false;
    }

    /// <summary>
    /// The existence-only counterpart of <see cref="Read"/>, and the only way either base class reaches
    /// <c>HasName</c>, so the agreement check sits in one place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Probe(INamedProjection projection, string name)
    {
        var has = projection.HasName(name);
        if (HostContractVerification.Enabled)
        {
            AssertHasNameAgreesWithTryGetNamedValue(projection, name, has);
        }

        return has;
    }

    /// <summary>
    /// The attribute triple a projected name reports: enumerability from <c>IsNameEnumerable</c>, writability
    /// from <c>IsNameWritable</c>, configurability forced to <see langword="true"/> because a live projection
    /// may stop carrying the name and a non-configurable property may never afterwards report as absent.
    /// </summary>
    internal static PropertyFlag FlagsFor(INamedProjection projection, string name)
    {
        var enumerable = projection.IsNameEnumerable(name);
        if (!projection.IsNameWritable(name))
        {
            if (HostContractVerification.Enabled)
            {
                AssertReadOnlyNameHasNoDeadWriteHook(projection, name);
            }

            return enumerable ? ReadOnlyEnumerable : ReadOnlyNonEnumerable;
        }

        return enumerable ? WritableEnumerable : WritableNonEnumerable;
    }

    /// <summary>
    /// The descriptor for a projected name — the only place one is built, so
    /// <see cref="ObjectInstance.GetOwnProperty"/> and <see cref="ObjectInstance.GetOwnProperties"/> cannot
    /// report different attributes for the same name.
    /// </summary>
    internal static PropertyDescriptor DescriptorFor(INamedProjection projection, string name, JsValue value)
        => new(value, FlagsFor(projection, name));

    /// <summary>
    /// The probe answer for a name the projection carries. Writability is deliberately not consulted: an
    /// <see cref="OwnPropertyProbe"/> answers existence and enumerability only, which is the whole reason it
    /// can skip the descriptor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OwnPropertyProbe ProbeResultFor(INamedProjection projection, string name)
        => projection.IsNameEnumerable(name) ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;

    /// <summary>
    /// Routes one <c>[[Set]]</c> to the host. <see langword="false"/> is a <b>refused</b> write, which the
    /// caller returns unchanged: the language turns that into a <c>TypeError</c> in strict mode and a silent
    /// no-op in sloppy mode, exactly as it does for an ordinary non-writable data property.
    /// </summary>
    internal static bool Write(INamedProjection projection, string name, JsValue value)
    {
        var written = projection.TrySetNamedValue(name, value);
        if (HostContractVerification.Enabled && !written)
        {
            AssertWritableNameHasAWriteHook(projection, name);
        }

        return written;
    }

    /// <summary>
    /// Routes one <c>[[Delete]]</c> to the host. <see langword="false"/> is a <b>refused</b> delete —
    /// <c>delete obj.name</c> evaluates to <c>false</c> in sloppy mode and throws a <c>TypeError</c> in strict
    /// mode — which is what the default <c>TryDeleteName</c> answers, so a projection that never overrode it
    /// behaves exactly as it did when the class had no delete hook at all.
    /// </summary>
    internal static bool Remove(INamedProjection projection, string name)
    {
        var deleted = projection.TryDeleteName(name);
        if (HostContractVerification.Enabled && deleted)
        {
            AssertDeletedNameIsGone(projection, name);
        }

        return deleted;
    }

    /// <summary>
    /// The projected names as keys, in <c>[[OwnPropertyKeys]]</c> order for the requested
    /// <paramref name="order"/>. For <see cref="NameOrder.IndexNamesFirst"/> the index list is allocated only
    /// when the projection actually carries a canonical-array-index name, which is the uncommon case.
    /// </summary>
    internal static List<JsValue> CollectNames(INamedProjection projection, Engine engine, NameOrder order)
    {
        var count = projection.NameCount;
        var keys = new List<JsValue>(count < 0 ? 0 : count);
        List<uint>? indices = null;

        for (var i = 0; i < count; i++)
        {
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                engine.Constraints.Check();
            }

            var name = projection.NameAt(i);
            if (name is null)
            {
                Throw.InvalidOperationException($"{projection.GetType()}.NameAt({i}) returned null; every name below NameCount must be a string.");
            }

            var arrayIndex = ArrayInstance.ParseArrayIndex(name);
            var isIndexName = arrayIndex < ArrayOperations.MaxArrayLength;

            if (order == NameOrder.BesideIndices)
            {
                // The collection owns every canonical array index and `length`; the read paths check those
                // before they ever reach the projection, so advertising one here would list a key whose read
                // resolves somewhere else.
                if (isIndexName || string.Equals(name, "length", StringComparison.Ordinal))
                {
                    if (HostContractVerification.Enabled)
                    {
                        HostContractVerification.Fail($"{projection.GetType()}.NameAt({i}) reported '{name}', which belongs to the indexed collection rather than to the named projection. Canonical array indices and 'length' are answered from Length/TryGetIndex before the projection is consulted, so this name is unreachable; rename it or drop it.");
                    }

                    continue;
                }

                keys.Add(JsString.Create(name));
                continue;
            }

            if (isIndexName)
            {
                (indices ??= new List<uint>()).Add(arrayIndex);
            }
            else
            {
                keys.Add(JsString.Create(name));
            }
        }

        if (indices is null)
        {
            if (HostContractVerification.Enabled)
            {
                AssertAdvertisedNamesExist(projection, keys);
            }

            return keys;
        }

        indices.Sort();
        var ordered = new List<JsValue>(indices.Count + keys.Count);
        foreach (var arrayIndex in indices)
        {
            ordered.Add(JsString.Create(arrayIndex));
        }

        ordered.AddRange(keys);
        if (HostContractVerification.Enabled)
        {
            AssertAdvertisedNamesExist(projection, ordered);
        }

        return ordered;
    }

    /// <summary>
    /// Whether an ordinary property-bag key is shadowed by the projection and must therefore be left out of
    /// the key enumerations — an expando written before the projection started carrying that name. Listing
    /// both would put a duplicate into <c>[[OwnPropertyKeys]]</c>. Nothing is asked at all in the usual case,
    /// where the projection carries no names or the bag is empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ShadowsBagKey(INamedProjection projection, JsValue key, int projectedCount)
        => projectedCount > 0 && key is JsString name && Probe(projection, name.ToString());

    /// <summary>
    /// Verifier for the <c>HasName</c> contract, checking <b>both</b> directions against
    /// <c>TryGetNamedValue</c> for the same name.
    /// </summary>
    private static void AssertHasNameAgreesWithTryGetNamedValue(INamedProjection projection, string name, bool answered)
    {
        var produced = projection.TryGetNamedValue(name, out _);
        if (produced == answered)
        {
            return;
        }

        HostContractVerification.Fail(answered
            ? $"{projection.GetType()}.HasName answered true for '{name}' but its TryGetNamedValue answers false. The engine trusts HasName, so this advertises a key whose read yields undefined or resolves on the prototype."
            : $"{projection.GetType()}.HasName answered false for '{name}' but its TryGetNamedValue produces a value. The engine trusts HasName, so this silently drops the property from `in`, hasOwnProperty, Object.keys, spread and JSON.stringify while obj['{name}'] still reads it.");
    }

    /// <summary>
    /// Verifier for the <c>NameAt</c> contract: every advertised name must be readable, and no name may
    /// repeat.
    /// </summary>
    private static void AssertAdvertisedNamesExist(INamedProjection projection, List<JsValue> names)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i].ToString();
            if (!seen.Add(name))
            {
                HostContractVerification.Fail($"{projection.GetType()}.NameAt reported '{name}' more than once. A duplicate key makes Object.keys and Object.getOwnPropertyNames report the property twice.");
            }

            if (!projection.TryGetNamedValue(name, out _))
            {
                HostContractVerification.Fail($"{projection.GetType()}.NameAt advertised '{name}' but its TryGetNamedValue answers false for it. Object.keys and Object.getOwnPropertyNames would list a key that reads as undefined or resolves on the prototype.");
            }
        }
    }

    /// <summary>
    /// Verifier for the half of the writability contract the engine cannot see coming: a name reported
    /// <c>writable: true</c> whose write the type has no hook to accept, so every assignment to it is refused
    /// while the descriptor advertises it as assignable.
    /// </summary>
    private static void AssertWritableNameHasAWriteHook(INamedProjection projection, string name)
    {
        var type = projection.GetType();
        if ((DeclaredHooks(type) & NamedProjectionHooks.Write) != NamedProjectionHooks.None)
        {
            // The hook exists and refused this particular write, which is a legitimate answer.
            return;
        }

        HostContractVerification.Fail($"{type}.IsNameWritable answered true for '{name}' but the type does not override TrySetNamedValue, so the write is refused — a TypeError in strict mode, a silent no-op in sloppy mode — while the property reports writable: true. Override TrySetNamedValue, or leave IsNameWritable at its default.");
    }

    /// <summary>
    /// The other half: a type that overrode <c>TrySetNamedValue</c> but reports every name non-writable, so
    /// the hook is dead code and every assignment is refused. Checked where the read-only attributes are
    /// actually built, and only for a type that declared <b>no</b> <c>IsNameWritable</c> override — a type
    /// that has one is entitled to answer <see langword="false"/> for the read-only names of a partly writable
    /// record.
    /// </summary>
    /// <remarks>
    /// <c>TryDeleteName</c> is deliberately not part of this check. Deletion is governed by
    /// <c>configurable</c>, which a projected name always reports <see langword="true"/>, so a projection whose
    /// names are read-only but removable is a perfectly ordinary shape and not a mistake.
    /// </remarks>
    private static void AssertReadOnlyNameHasNoDeadWriteHook(INamedProjection projection, string name)
    {
        var type = projection.GetType();
        var hooks = DeclaredHooks(type);
        if ((hooks & NamedProjectionHooks.Writable) != NamedProjectionHooks.None
            || (hooks & NamedProjectionHooks.Write) == NamedProjectionHooks.None)
        {
            return;
        }

        HostContractVerification.Fail($"{type} overrides TrySetNamedValue but never overrides IsNameWritable, so '{name}' — and every other projected name — reports writable: false and the write hook is never consulted. Override IsNameWritable to say which names are assignable.");
    }

    /// <summary>
    /// Verifier for <c>TryDeleteName</c>: <c>[[Delete]]</c> answering <see langword="true"/> means the
    /// property is gone, so the projection must no longer carry the name.
    /// </summary>
    private static void AssertDeletedNameIsGone(INamedProjection projection, string name)
    {
        if (!projection.TryGetNamedValue(name, out _))
        {
            return;
        }

        HostContractVerification.Fail($"{projection.GetType()}.TryDeleteName answered true for '{name}' but its TryGetNamedValue still produces a value for it. `delete obj['{name}']` reported success for a property that is still there, and the next read still sees it.");
    }

    /// <summary>
    /// Which optional hooks a host type declared, cached per <see cref="Type"/> and shared process-wide for
    /// the same reason <c>ObjectInstance</c>'s semantics cache is: the answer depends only on the type, and an
    /// entry retains nothing but its <see cref="Type"/> key.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, NamedProjectionHooks> _declaredHooks = new();

    internal static NamedProjectionHooks DeclaredHooks(Type type)
        => _declaredHooks.TryGetValue(type, out var hooks) ? hooks : DeclaredHooksUncached(type);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static NamedProjectionHooks DeclaredHooksUncached(Type type)
        => _declaredHooks.GetOrAdd(type, static t => ProbeDeclaredHooks(t));

    /// <summary>
    /// Reads the eight named hooks off the runtime type once. Used for two things, both of which are safe when
    /// the metadata is unavailable: routing (<see cref="NamedProjectionHooks.Any"/>, where an inconclusive
    /// answer must be <em>optimistic</em>, so the fallback claims every hook and merely costs the calls a
    /// declared projection would have cost), and the writability verifiers above, which run only when
    /// host-contract verification is on.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Reads well-known virtual members of this very type's own hierarchy, which the trimmer keeps because the engine calls them virtually. If the metadata is unavailable the probe claims every hook, which only costs the virtual calls a declared projection would have cost and never changes an answer.")]
    private static NamedProjectionHooks ProbeDeclaredHooks(Type type)
    {
        const BindingFlags Lookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var jint = typeof(ObjectInstance).Assembly;

        var hooks = NamedProjectionHooks.None;

        try
        {
            if (DeclaresOutside(type.GetProperty(nameof(INamedProjection.NameCount), Lookup)?.GetMethod, jint)
                || DeclaresOutside(type.GetMethod(nameof(INamedProjection.NameAt), Lookup, binder: null, types: [typeof(int)], modifiers: null), jint)
                || DeclaresOutside(type.GetMethod(nameof(INamedProjection.TryGetNamedValue), Lookup, binder: null, types: [typeof(string), typeof(JsValue).MakeByRefType()], modifiers: null), jint)
                || DeclaresOutside(type.GetMethod(nameof(INamedProjection.HasName), Lookup, binder: null, types: [typeof(string)], modifiers: null), jint)
                || DeclaresOutside(type.GetMethod(nameof(INamedProjection.IsNameEnumerable), Lookup, binder: null, types: [typeof(string)], modifiers: null), jint))
            {
                hooks |= NamedProjectionHooks.Any;
            }

            if (DeclaresOutside(type.GetMethod(nameof(INamedProjection.IsNameWritable), Lookup, binder: null, types: [typeof(string)], modifiers: null), jint))
            {
                hooks |= NamedProjectionHooks.Any | NamedProjectionHooks.Writable;
            }

            if (DeclaresOutside(type.GetMethod(nameof(INamedProjection.TrySetNamedValue), Lookup, binder: null, types: [typeof(string), typeof(JsValue)], modifiers: null), jint))
            {
                hooks |= NamedProjectionHooks.Any | NamedProjectionHooks.Write;
            }

            if (DeclaresOutside(type.GetMethod(nameof(INamedProjection.TryDeleteName), Lookup, binder: null, types: [typeof(string)], modifiers: null), jint))
            {
                // Only Any: nothing verifies TryDeleteName against a declared attribute, because deletion is
                // governed by `configurable`, which a projected name always reports true.
                hooks |= NamedProjectionHooks.Any;
            }
        }
        catch (Exception e) when (e is MissingMemberException or NotSupportedException or AmbiguousMatchException)
        {
            return NamedProjectionHooks.Any | NamedProjectionHooks.Writable | NamedProjectionHooks.Write;
        }

        return hooks;
    }

    private static bool DeclaresOutside(MethodInfo? method, Assembly jint)
        => method?.DeclaringType is { } declaringType && declaringType.Assembly != jint;
}
