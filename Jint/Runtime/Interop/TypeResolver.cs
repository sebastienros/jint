using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop;

/// <summary>
/// Interop strategy for resolving types and members.
/// </summary>
/// <remarks>
/// Holds the cache of resolved CLR member accessors, so the same instance should be shared between engines:
/// resolving a member — and compiling the delegates that read and write it — then happens once per member
/// rather than once per member per engine, which matters most for embedders that construct a fresh engine per
/// operation. The cache keeps the reflected <see cref="Type"/>s alive for as long as the resolver lives, and
/// <see cref="Default"/> lives for the process; give a resolver of your own to engines that must not outlive
/// the types they touch.
/// </remarks>
public sealed class TypeResolver
{
    public static readonly TypeResolver Default = new();

    private readonly ConcurrentDictionary<AccessorCacheKey, ReflectionAccessor> _reflectionAccessors = new();
    private readonly ConcurrentDictionary<StaticAccessorCacheKey, ReflectionAccessor> _staticAccessors = new();
    private readonly ConcurrentDictionary<Type, MethodDescriptor[]> _constructors = new();

    /// <summary>
    /// Memo behind <see cref="ConverterProbedIndexKeyTypes"/>, populated only for engines that installed a
    /// <see cref="ClrTypeConverter"/> of their own and therefore have to ask the question at all.
    /// </summary>
    private readonly ConcurrentDictionary<Type, Type[]> _converterProbedIndexKeyTypes = new();

    /// <summary>
    /// Memo behind <see cref="ExposesIndexedElements"/>, populated only for resolvers carrying a
    /// <see cref="MemberFilter"/> of the host's own and therefore having a decision to make at all.
    /// </summary>
    private readonly ConcurrentDictionary<Type, bool> _indexedElementExposure = new();

    /// <summary>
    /// How many accessors this resolver currently holds. The cache never evicts, so this is the retention
    /// the resolver commits to: it must stay bounded by the distinct members the engines using it resolve,
    /// and must not grow with the number of engines constructed.
    /// </summary>
    internal int ResolvedAccessorCount => _reflectionAccessors.Count + _staticAccessors.Count;

    private Predicate<MemberInfo> _memberFilter = static _ => true;
    private bool _memberFilterIsDefault = true;
    private Func<MemberInfo, IEnumerable<string>> _memberNameCreator = NameCreator;
    private StringComparer _memberNameComparer = DefaultMemberNameComparer.Instance;
    private bool _memberNameCreatorIsDefault = true;

    /// <summary>
    /// The last <see cref="JsAccessibleRegistry"/> generation this resolver's cache was known to be consistent
    /// with. A registration landing after a member of the same type was already resolved would otherwise keep
    /// being answered from the cache with the reflected accessor, which is the shape of bug a host cannot
    /// diagnose: the generated lane is simply never taken and nothing says so.
    /// </summary>
    private int _generatedMembersGeneration = JsAccessibleRegistry.Generation;

    /// <summary>The default member-name comparer, which is also how the generated member tables are keyed.</summary>
    internal static StringComparer DefaultNameComparer => DefaultMemberNameComparer.Instance;

    /// <summary>
    /// Registers a filter that determines whether a type or member is exposed to interop or returned as undefined.
    /// By default allows all but will also be limited by <see cref="Options.InteropOptions.AllowGetType"/> configuration.
    /// </summary>
    /// <remarks>
    /// Assigning a different filter discards everything this resolver has resolved so far — every cached
    /// accessor was produced under the previous filter, and a member it exposed (or hid) would otherwise keep
    /// being served from the cache to engines that ask afterwards, in this engine and in every other one
    /// sharing the resolver. Assigning the same instance back costs nothing.
    /// <para>
    /// <see cref="NamespaceReference"/> consults the filter for each top-level or nested type it discovers.
    /// A root <see cref="TypeReference"/> explicitly exported by the host is already a capability and bypasses
    /// that type check, but its constructors, static members, and nested types still use this filter.
    /// </para>
    /// </remarks>
    /// <seealso cref="Options.InteropOptions.AllowGetType"/>
    public Predicate<MemberInfo> MemberFilter
    {
        get => _memberFilter;
        set
        {
            if (!ReferenceEquals(_memberFilter, value))
            {
                _memberFilter = value;
                _memberFilterIsDefault = false;
                InvalidateResolvedAccessors();
            }
        }
    }

    /// <summary>
    /// Drops every accessor resolved so far. Called when a setting that steers resolution changes, since a
    /// cached entry only stays valid for as long as the settings it was resolved under do.
    /// </summary>
    /// <remarks>
    /// A resolution already in flight on another thread can still land its entry afterwards; that is inherent
    /// to mutating the settings while engines are running and no worse than the write ordering a host would
    /// have had to reason about anyway. Mutate the settings before handing the resolver to an engine when
    /// that matters.
    /// </remarks>
    private void InvalidateResolvedAccessors()
    {
        _reflectionAccessors.Clear();
        _staticAccessors.Clear();
        _constructors.Clear();
        _converterProbedIndexKeyTypes.Clear();
        _indexedElementExposure.Clear();
    }

    /// <summary>
    /// Whether <see cref="MemberFilter"/> admits the integer indexer an array-like view of
    /// <paramref name="type"/> reaches its elements through, and therefore whether that view has element
    /// properties at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A wrapped collection's element lanes do not resolve a member per access — that is the whole point of
    /// <c>ArrayLikeWrapper</c>, which owns every index-shaped key so an out-of-range write cannot become the
    /// collection's own <see cref="ArgumentOutOfRangeException"/> — so the filter has to be consulted about
    /// the member those lanes stand for, once, rather than per element. The member is the one
    /// <see cref="IndexerAccessor.TryFindIndexer"/> would have selected: the first integer-keyed indexer the
    /// exposed type itself declares, falling back to <paramref name="descriptorIndexer"/> for a
    /// <c>T[]</c>, which declares none of its own and reaches its elements through <c>IList.Item</c>. A type
    /// offering no integer indexer at all leaves nothing to ask, and its view keeps its elements.
    /// </para>
    /// <para>
    /// The answer is memoized per resolver and per type, and is engine-independent: the only part of
    /// <see cref="Filter"/> an engine steers is <see cref="Options.InteropOptions.AllowGetType"/>, which
    /// gates the name <c>GetType</c>, and the one shape that could carry it — an indexer renamed by
    /// <c>[IndexerName("GetType")]</c> — is excluded from the memo rather than assumed away.
    /// </para>
    /// </remarks>
    internal bool ExposesIndexedElements(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        PropertyInfo? descriptorIndexer)
    {
        if (_memberFilterIsDefault)
        {
            // the default filter admits everything, so there is no decision to look up and none to cache
            return true;
        }

        if (_indexedElementExposure.TryGetValue(type, out var exposed))
        {
            return exposed;
        }

        var indexer = DeclaredIntegerIndexer(type) ?? descriptorIndexer;
        exposed = indexer is null || Filter(engine, type, indexer);

        if (indexer is null || !string.Equals(indexer.Name, nameof(GetType), StringComparison.Ordinal))
        {
            // GetOrAdd's value overload rather than TryAdd, so a race still hands every caller the same
            // answer the dictionary holds
            return _indexedElementExposure.GetOrAdd(type, exposed);
        }

        return exposed;
    }

    /// <summary>
    /// The first integer-keyed single-parameter indexer <paramref name="type"/> declares itself, scanned the
    /// way <see cref="IndexerAccessor.TryFindIndexer"/> scans — an explicitly implemented one is reported by
    /// its interface alone and is deliberately not found here, exactly as it is not found there.
    /// </summary>
    private static PropertyInfo? DeclaredIntegerIndexer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        foreach (var candidate in type.GetProperties())
        {
            var indexParameters = candidate.GetIndexParameters();
            if (indexParameters.Length == 1 && ObjectWrapper.IsIntegerIndexParameter(indexParameters[0].ParameterType))
            {
                return candidate;
            }
        }

        return null;
    }

    internal bool Filter(Engine engine, Type targetType, MemberInfo m)
    {
        // some specific problematic indexer cases for JSON interop
        if (string.Equals(m.Name, "Item", StringComparison.Ordinal) && m is PropertyInfo p)
        {
            var indexParameters = p.GetIndexParameters();
            if (indexParameters.Length == 1)
            {
                var parameter = indexParameters[0];
                if (string.Equals(m.DeclaringType?.FullName, "System.Text.Json.Nodes.JsonNode", StringComparison.Ordinal))
                {
                    // STJ
                    return parameter.ParameterType == typeof(string) && string.Equals(targetType.FullName, "System.Text.Json.Nodes.JsonObject", StringComparison.Ordinal)
                           || parameter.ParameterType == typeof(int) && string.Equals(targetType.FullName, "System.Text.Json.Nodes.JsonArray", StringComparison.Ordinal);
                }

                if (string.Equals(targetType.FullName, "Newtonsoft.Json.Linq.JArray", StringComparison.Ordinal))
                {
                    // NJ
                    return parameter.ParameterType == typeof(int);
                }
            }
        }

        if (m is MethodInfo { IsStatic: true, DeclaringType: not null } method
            && method.DeclaringType == typeof(Type)
            && string.Equals(method.Name, nameof(Type.GetType), StringComparison.Ordinal))
        {
            return false;
        }

        return (AllowGetType(engine) || !string.Equals(m.Name, nameof(GetType), StringComparison.Ordinal)) && _memberFilter(m);
    }

    /// <summary>
    /// Whether a type discovered through a namespace is part of the host's type allow-list. Explicitly exported
    /// <see cref="TypeReference"/> values do not use this check.
    /// </summary>
    internal bool FilterType(Type type) => _memberFilter(type);

    /// <summary>
    /// Gives the exposed names for a member. Allows to expose C# convention following member like IsSelected
    /// as more JS idiomatic "selected" for example. Defaults to returning the <see cref="MemberInfo.Name"/> as-is.
    /// </summary>
    /// <remarks>
    /// Assigning a different name creator discards everything this resolver has resolved so far, for the
    /// reason given on <see cref="MemberFilter"/>.
    /// </remarks>
    public Func<MemberInfo, IEnumerable<string>> MemberNameCreator
    {
        get => _memberNameCreator;
        set
        {
            if (!ReferenceEquals(_memberNameCreator, value))
            {
                _memberNameCreator = value;
                _memberNameCreatorIsDefault = false;
                InvalidateResolvedAccessors();
            }
        }
    }

    private static IEnumerable<string> NameCreator(MemberInfo info)
    {
        yield return info.Name;
    }

    /// <summary>
    /// Sets member name comparison strategy when finding CLR objects members.
    /// By default member's first character casing is ignored and rest of the name is compared with strict equality.
    /// </summary>
    /// <remarks>
    /// Assigning a different comparer discards everything this resolver has resolved so far, for the reason
    /// given on <see cref="MemberFilter"/>.
    /// </remarks>
    public StringComparer MemberNameComparer
    {
        get => _memberNameComparer;
        set
        {
            if (!ReferenceEquals(_memberNameComparer, value))
            {
                _memberNameComparer = value;
                InvalidateResolvedAccessors();
            }
        }
    }

    /// <summary>
    /// The interop settings that steer member resolution but live on the engine's options rather than on this
    /// resolver, read from the profile the engine captured so that a resolution and the cache entry it
    /// produces can never disagree about them. An engine still inside its constructor has not captured a
    /// profile yet and reads the live options, which is correct there: nothing resolved in that window enters
    /// the cache (see <see cref="GetAccessor"/>).
    /// </summary>
    private static bool AllowGetType(Engine engine)
    {
        ref readonly var profile = ref engine._interopResolutionProfile;
        return profile.IsCaptured ? profile.AllowGetType : engine.Options.Interop.AllowGetType;
    }

    /// <inheritdoc cref="AllowGetType"/>
    private static BindingFlags FieldBindingFlags(Engine engine)
    {
        ref readonly var profile = ref engine._interopResolutionProfile;
        return profile.IsCaptured ? profile.FieldBindingFlags : engine.Options.Interop.ObjectWrapperReportedFieldBindingFlags;
    }

    /// <inheritdoc cref="AllowGetType"/>
    private static BindingFlags PropertyBindingFlags(Engine engine)
    {
        ref readonly var profile = ref engine._interopResolutionProfile;
        return profile.IsCaptured ? profile.PropertyBindingFlags : engine.Options.Interop.ObjectWrapperReportedPropertyBindingFlags;
    }

    /// <inheritdoc cref="AllowGetType"/>
    private static BindingFlags MethodBindingFlags(Engine engine)
    {
        ref readonly var profile = ref engine._interopResolutionProfile;
        return profile.IsCaptured ? profile.MethodBindingFlags : engine.Options.Interop.ObjectWrapperReportedMethodBindingFlags;
    }

    internal ReflectionAccessor GetAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes)] Type type,
        string member,
        MemberResolutionRequirement requirement,
        bool throwOnError = true,
        Func<ReflectionAccessor?>? accessorFactory = null)
    {
        if (accessorFactory is not null)
        {
            // The caller picked the member itself — ObjectWrapper.GetPropertyDescriptor hands in a MemberInfo
            // — so what the factory builds is not what ordinary resolution would produce for this
            // (type, name): a single overload rather than the whole set, for one. The key records nothing of
            // that origin, so a factory result must neither be stored, where it would answer later ordinary
            // reads with the host's narrower member, nor be answered from, where a warm entry would silently
            // ignore the MemberInfo the host passed. Bypass both directions; the factory is cheap and this
            // call site is per-request rather than per property access.
            var supplied = accessorFactory();
            if (supplied is not null)
            {
                return supplied;
            }
        }

        SyncGeneratedMembers();

        var profile = engine._interopResolutionProfile;
        if (!profile.IsCaptured)
        {
            // The engine is still running its configuration callbacks and has not captured the profile that
            // partitions the cache yet — the extension method lookup, for one, is only final once they have
            // all run. Resolve without touching the cache rather than risk mislabeling an entry. This also
            // covers the converter's own window: a callback installs it, so anything resolved before that
            // point neither enters the cache nor is answered from it.
            return ResolvePropertyDescriptorFactory(engine, type, member, requirement, throwOnError);
        }

        var key = new AccessorCacheKey(type, member, requirement, profile);

        var factories = _reflectionAccessors;
        if (factories.TryGetValue(key, out var accessor) && IsConverterNeutral(engine, type))
        {
            if (throwOnError
                && ReferenceEquals(accessor, ConstantValueAccessor.NullAccessor)
                && engine.Options.Interop.ThrowOnUnresolvedMember)
            {
                throw CreateMissingMemberException(engine, type, member);
            }
            return accessor;
        }

        accessor = ResolvePropertyDescriptorFactory(engine, type, member, requirement, throwOnError);

        // don't cache if numeric indexer
        if (uint.TryParse(member, out _))
        {
            return accessor;
        }

        if (IsShareable(engine, type, accessor))
        {
            // racy, we don't care: both racers resolved the same member the same way
            factories.TryAdd(key, accessor);
        }

        return accessor;
    }

    internal ReflectionAccessor GetStaticAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes)]
        Type type,
        string member)
    {
        var profile = engine._interopResolutionProfile;
        if (!profile.IsCaptured)
        {
            return ResolveStaticAccessor(engine, type, member);
        }

        // No converter-neutrality question on this lane. ResolveStaticAccessor goes through
        // TryFindMemberAccessor alone, passing no indexer to try, and that method never probes one — so
        // nothing here ever consults the engine's ClrTypeConverter and every entry answers every engine.
        var key = new StaticAccessorCacheKey(type, member, profile);
        if (_staticAccessors.TryGetValue(key, out var accessor))
        {
            return accessor;
        }

        accessor = ResolveStaticAccessor(engine, type, member);
        if (accessor is not NestedTypeAccessor)
        {
            _staticAccessors.TryAdd(key, accessor);
        }

        return accessor;
    }

    internal MethodDescriptor[] GetConstructors(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        // Resolved here rather than in a GetOrAdd lambda: a lambda parameter carries no
        // [DynamicallyAccessedMembers], so the caller's promise about `type` was dropped at the closure
        // boundary and the GetConstructors call read as unannotated in every trimming build.
        if (_constructors.TryGetValue(type, out var cached))
        {
            return cached;
        }

        List<ConstructorInfo> constructors = [.. type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)];
        constructors.RemoveAll(x => !Filter(engine, type, x));

        // GetOrAdd's value overload rather than TryAdd, so that a race still hands every caller the same
        // array the dictionary holds - which is what the factory overload guaranteed.
        return _constructors.GetOrAdd(type, MethodDescriptor.Build(constructors));
    }

    private ReflectionAccessor ResolveStaticAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes)]
        Type type,
        string member)
    {
        const BindingFlags BindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return TryFindMemberAccessor(engine, type, member, BindingFlags, indexerToTry: null, out var accessor)
            ? accessor
            : ConstantValueAccessor.NullAccessor;
    }

    /// <summary>
    /// Whether an accessor may enter the cache this resolver shares between engines. Everything a
    /// <see cref="ReflectionAccessor"/> normally holds is derived from the reflected type alone — a
    /// <see cref="PropertyInfo"/>/<see cref="FieldInfo"/> plus the delegates compiled from it, a
    /// <see cref="MethodDescriptor"/> set, a converted indexer key — and the engine-affine parts are built
    /// per call in <see cref="ReflectionAccessor.CreatePropertyDescriptor"/>. The two exceptions are below.
    /// </summary>
    private bool IsShareable(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        ReflectionAccessor accessor)
    {
        // A nested type resolves to a TypeReference, which is a JsValue owned by the engine that created it:
        // sharing it would hand one engine's object to another and pin that engine for the resolver's lifetime.
        if (accessor is NestedTypeAccessor)
        {
            return false;
        }

        return IsConverterNeutral(engine, type);
    }

    /// <summary>
    /// Whether a member of <paramref name="type"/> resolves identically under the stock
    /// <see cref="ClrTypeConverter"/> and under <paramref name="engine"/>'s, and may therefore be both stored
    /// in and served from the cache this resolver shares between engines whose converters differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolution consults the converter in exactly one place — <see cref="IndexerAccessor.TryFindIndexer"/>,
    /// which asks whether the member <em>name</em> converts to an indexer's index type — and that single
    /// answer decides four things: whether an <see cref="IndexerAccessor"/> is produced at all and what index
    /// key it bakes in; whether the declared property or field accessor is handed an indexer to probe, which
    /// <see cref="ReflectionAccessor.GetValue"/> probes <b>before</b> the declared member; whether a
    /// <c>[JsAccessible]</c> type may use its generated lane, which is gated on there being no such indexer;
    /// and whether resolution ends at "no such member" rather than at the indexer.
    /// </para>
    /// <para>
    /// Only the first of those is visible in the resolved artefact, which is why the question is asked of the
    /// <paramref name="type"/> instead of the accessor: an entry is neutral when this engine's converter is
    /// never consulted about any index key type the type could offer. The answer has to be given on the
    /// <b>read</b> side as well as the write side, and in both directions — an entry a stock engine stored
    /// must not be served to an engine whose converter would have answered differently, nor the reverse.
    /// A converter that declared its target types is asked only about those, so declaring (say)
    /// <c>TimeSpan</c> costs nothing on a <c>string</c>-keyed dictionary.
    /// </para>
    /// </remarks>
    private bool IsConverterNeutral(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        var filter = engine._typeConverterTargetFilter;
        if (filter is null)
        {
            // The stock converter is the reference every other engine's neutrality is measured against, so an
            // ordinary engine pays one null check here and nothing else - on the read side as on the write one.
            return true;
        }

        foreach (var indexKeyType in ConverterProbedIndexKeyTypes(type))
        {
            if (filter.Claims(indexKeyType))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The index key types a <see cref="ClrTypeConverter"/> can be consulted about while resolving a member
    /// of <paramref name="type"/>: the index parameter types of every single-parameter indexer the type or
    /// one of its interfaces declares, minus <see cref="int"/>, which
    /// <see cref="IndexerAccessor.TryFindIndexer"/> keys without asking anyone.
    /// </summary>
    /// <remarks>
    /// Deliberately computed without <see cref="MemberFilter"/>, which the probe itself applies: the filter
    /// belongs to the resolver rather than to the type, and answering conservatively only ever costs an
    /// engine a cache entry it could have shared. Memoized per resolver rather than per process so it keeps
    /// the reflected types alive on exactly the terms the accessor cache already does, and reached only by
    /// engines that installed a converter of their own.
    /// </remarks>
    private Type[] ConverterProbedIndexKeyTypes(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        if (_converterProbedIndexKeyTypes.TryGetValue(type, out var cached))
        {
            return cached;
        }

        List<Type>? found = null;
        Collect(type, ref found);

        // ResolvePropertyDescriptorFactory probes each interface separately, and an explicitly implemented
        // indexer is reported by the interface alone.
        foreach (var iface in type.GetInterfaces())
        {
            Collect(iface, ref found);
        }

        var indexKeyTypes = found is null ? [] : found.ToArray();
        _converterProbedIndexKeyTypes[type] = indexKeyTypes;
        return indexKeyTypes;

        static void Collect(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type declaringType,
            ref List<Type>? found)
        {
            foreach (var candidate in declaringType.GetProperties())
            {
                var indexParameters = candidate.GetIndexParameters();
                if (indexParameters.Length != 1)
                {
                    continue;
                }

                var indexKeyType = indexParameters[0].ParameterType;
                if (indexKeyType == typeof(int) || found?.Contains(indexKeyType) == true)
                {
                    continue;
                }

                found ??= [];
                found.Add(indexKeyType);
            }
        }
    }

    /// <summary>
    /// Drops the accessor cache entries a <see cref="JsAccessibleRegistry"/> registration can have changed,
    /// when something has been registered since the last resolution. Costs one volatile read per resolution
    /// and nothing else while no host has registered anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The entries of registered types only, because a registration reaches nothing else: every consult of
    /// the registry is keyed on the exact target type, and the substitution additionally requires the
    /// reflected member to be declared on it. Dropping the whole cache instead was measurable from an
    /// unrelated engine — one <c>RegisterAll()</c> made every other type re-resolve, in every engine sharing
    /// any resolver, including a privately constructed one (#3368).
    /// </para>
    /// <para>
    /// The static accessors and the constructors are not touched at all. Everything registered is a public
    /// <em>instance</em> member, and the static lane passes its own binding flags — which is exactly what
    /// excludes it from the substitution in <see cref="TryFindMemberAccessor"/>.
    /// </para>
    /// </remarks>
    private void SyncGeneratedMembers()
    {
        var generation = JsAccessibleRegistry.Generation;
        if (_generatedMembersGeneration == generation)
        {
            return;
        }

        _generatedMembersGeneration = generation;

        foreach (var entry in _reflectionAccessors)
        {
            if (JsAccessibleRegistry.IsRegistered(entry.Key.Type))
            {
                _reflectionAccessors.TryRemove(entry.Key, out _);
            }
        }
    }

    /// <summary>
    /// Whether the registry's own name-keyed lookup answers what the reflected selection would have chosen,
    /// so a generated member can be resolved without reflecting over the type at all. It does exactly while
    /// the host has changed nothing that steers the selection: the registry is keyed by CLR name under
    /// <see cref="DefaultMemberNameComparer"/>, and it holds public instance members only.
    /// </summary>
    /// <remarks>
    /// A host that has changed one of them still gets the generated lanes — through
    /// <see cref="TryFindMemberAccessor"/>, which runs the whole reflected selection and swaps the generated
    /// accessor in for the member that selection landed on. This is only about whether the cheaper answer is
    /// also the same answer.
    /// </remarks>
    private bool GeneratedNameLookupIsEquivalent(Engine engine)
    {
        if (!_memberFilterIsDefault
            || !_memberNameCreatorIsDefault
            || !ReferenceEquals(_memberNameComparer, DefaultMemberNameComparer.Instance))
        {
            return false;
        }

        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        return (PropertyBindingFlags(engine) & PublicInstance) == PublicInstance
               && (FieldBindingFlags(engine) & PublicInstance) == PublicInstance
               && (MethodBindingFlags(engine) & PublicInstance) == PublicInstance;
    }

    /// <summary>
    /// The generated lane for the member the reflected selection just chose, if that member is a registered
    /// one and the two accessors answer the same questions about it. Reflection has already applied
    /// <see cref="MemberFilter"/>, <see cref="MemberNameCreator"/>, <see cref="MemberNameComparer"/> and the
    /// engine's binding flags by the time this is asked, so containment is honoured by construction and only
    /// the read and write lanes are exchanged.
    /// </summary>
    private static bool TrySubstituteGeneratedAccessor(
        Type type,
        MemberInfo reflectedMember,
        ReflectionAccessor reflected,
        [NotNullWhen(true)] out ReflectionAccessor? generated)
    {
        generated = null;

        if (JsAccessibleRegistry.IsEmpty
            || !JsAccessibleRegistry.TryGetAccessorForDeclaredMember(type, reflectedMember, out var candidate))
        {
            return false;
        }

        // Resolution is filtered by MemberResolutionRequirement further up, so a generated accessor that
        // disagreed with the reflected one about being readable or writable could turn a resolved member
        // into an unresolved one. The generator declines every shape where they could disagree; this says so
        // rather than trusting it.
        if (candidate.Readable != reflected.Readable || candidate.Writable != reflected.Writable)
        {
            return false;
        }

        generated = candidate;
        return true;
    }

    private ReflectionAccessor ResolvePropertyDescriptorFactory(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes)] Type type,
        string memberName,
        MemberResolutionRequirement requirement,
        bool throwOnError)
    {
        var isInteger = long.TryParse(memberName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

        // we can always check indexer if there's one, and then fall back to properties if indexer returns null
        IndexerAccessor.TryFindIndexer(engine, type, memberName, out var indexerAccessor, out var indexer);

        // A [JsAccessible] type's registered members stand in for exactly the step below - the declared
        // property/field/method lookup - and for nothing else, which is why they are consulted here rather
        // than ahead of the indexer probe: an annotated type carrying a string indexer must still resolve
        // names the way an un-annotated one does.
        //
        // This is the shortcut, taken only while the registry's own name-keyed lookup provably answers what
        // the reflected selection would have. Everything else reaches the same generated accessors through
        // TryFindMemberAccessor, which applies the host's filter and name policy first and substitutes
        // afterwards; the answer is the same, the cost is one reflected selection instead of none.
        if (!isInteger
            && indexer is null
            && !JsAccessibleRegistry.IsEmpty
            && GeneratedNameLookupIsEquivalent(engine)
            && JsAccessibleRegistry.TryGetMember(type, memberName, out var generated)
            // the one thing a default MemberFilter still decides, spelled the way Filter spells it
            && (AllowGetType(engine) || !string.Equals(generated.Name, nameof(GetType), StringComparison.Ordinal))
            && requirement.IsSatisfiedBy(generated.Accessor))
        {
            return generated.Accessor;
        }

        // properties and fields cannot be numbers
        if (!isInteger
            && TryFindMemberAccessor(engine, type, memberName, bindingFlags: null, indexer, out var temp)
            && requirement.IsSatisfiedBy(temp))
        {
            return temp;
        }

        if (typeof(DynamicObject).IsAssignableFrom(type))
        {
            return new DynamicObjectAccessor();
        }

        var typeResolverMemberNameComparer = _memberNameComparer;
        var typeResolverMemberNameCreator = _memberNameCreator;

        if (!isInteger)
        {
            // try to find a single explicit property implementation
            List<PropertyInfo>? list = null;
            foreach (var iface in type.GetInterfaces())
            {
                foreach (var iprop in iface.GetProperties())
                {
                    if (!Filter(engine, type, iprop))
                    {
                        continue;
                    }

                    if (string.Equals(iprop.Name, "Item", StringComparison.Ordinal) && iprop.GetIndexParameters().Length == 1)
                    {
                        // never take indexers, should use the actual indexer
                        continue;
                    }

                    foreach (var name in typeResolverMemberNameCreator(iprop))
                    {
                        if (typeResolverMemberNameComparer.Equals(name, memberName))
                        {
                            list ??= new List<PropertyInfo>();
                            list.Add(iprop);
                        }
                    }
                }
            }

            if (list?.Count == 1)
            {
                return new PropertyAccessor(list[0]);
            }

            // try to find explicit method implementations
            List<MethodInfo>? explicitMethods = null;
            foreach (var iface in type.GetInterfaces())
            {
                foreach (var imethod in iface.GetMethods())
                {
                    if (!Filter(engine, type, imethod))
                    {
                        continue;
                    }

                    foreach (var name in typeResolverMemberNameCreator(imethod))
                    {
                        if (typeResolverMemberNameComparer.Equals(name, memberName)
                            && !ContainsMethodWithSameSignature(explicitMethods, imethod))
                        {
                            explicitMethods ??= new List<MethodInfo>();
                            explicitMethods.Add(imethod);
                        }
                    }
                }
            }

            if (explicitMethods?.Count > 0)
            {
                return new MethodAccessor(type, MethodDescriptor.Build(explicitMethods));
            }
        }

        // if no methods are found check if target implemented indexing
        var score = int.MaxValue;
        if (indexerAccessor != null)
        {
            var parameter = indexerAccessor.FirstIndexParameter;
            score = CalculateIndexerScore(parameter, isInteger);
        }

        if (score != 0)
        {
            // try to find explicit indexer implementations that has a better score than earlier
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (IndexerAccessor.TryFindIndexer(engine, interfaceType, memberName, out var accessor, out _))
                {
                    // ensure that original type is allowed against indexer
                    if (!Filter(engine, type, accessor.Indexer))
                    {
                        continue;
                    }

                    var parameter = accessor.FirstIndexParameter;
                    var newScore = CalculateIndexerScore(parameter, isInteger);
                    if (newScore < score)
                    {
                        // found a better one
                        indexerAccessor = accessor;
                        score = newScore;
                    }
                }
            }
        }

        // use the best indexer we were able to find
        if (indexerAccessor != null)
        {
            return indexerAccessor;
        }

        if (!isInteger && engine._extensionMethods.TryGetExtensionMethods(type, out var extensionMethods))
        {
            var matches = new List<MethodInfo>();
            foreach (var method in extensionMethods)
            {
                if (!Filter(engine, type, method))
                {
                    continue;
                }

                foreach (var name in typeResolverMemberNameCreator(method))
                {
                    if (typeResolverMemberNameComparer.Equals(name, memberName))
                    {
                        matches.Add(method);
                    }
                }
            }

            if (matches.Count > 0)
            {
                return new MethodAccessor(type, MethodDescriptor.Build(matches));
            }
        }

        if (throwOnError && engine.Options.Interop.ThrowOnUnresolvedMember)
        {
            throw CreateMissingMemberException(engine, type, memberName);
        }

        return ConstantValueAccessor.NullAccessor;
    }

    internal static MissingMemberException CreateMissingMemberException(Engine engine, Type type, string member)
    {
        var message = engine.Options.Interop.ExposeDetailedResolutionErrors
            ? $"Cannot access property '{member}' on type '{type.FullName}"
            : "Cannot access the requested CLR member.";
        return new MissingMemberException(message);
    }

    private static bool ContainsMethodWithSameSignature(List<MethodInfo>? methods, MethodInfo method)
    {
        if (methods is null)
        {
            return false;
        }

        foreach (var existing in methods)
        {
            if (HasSameSignature(existing, method))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSameSignature(MethodInfo a, MethodInfo b)
    {
        if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)
            || a.IsGenericMethodDefinition != b.IsGenericMethodDefinition)
        {
            return false;
        }

        if (a.IsGenericMethodDefinition && a.GetGenericArguments().Length != b.GetGenericArguments().Length)
        {
            return false;
        }

        var parametersA = a.GetParameters();
        var parametersB = b.GetParameters();
        if (parametersA.Length != parametersB.Length)
        {
            return false;
        }

        for (var i = 0; i < parametersA.Length; i++)
        {
            if (!TypesMatch(parametersA[i].ParameterType, parametersB[i].ParameterType))
            {
                return false;
            }
        }

        return TypesMatch(a.ReturnType, b.ReturnType);
    }

    /// <summary>
    /// Structural type equality that treats positionally identical generic method parameters as equal,
    /// the same method seen through both a class and an interface has distinct generic parameter instances.
    /// </summary>
    private static bool TypesMatch(Type a, Type b)
    {
        if (a == b)
        {
            return true;
        }

        if (a.IsGenericParameter || b.IsGenericParameter)
        {
            return a.IsGenericParameter
                && b.IsGenericParameter
                && a.DeclaringMethod is not null
                && b.DeclaringMethod is not null
                && a.GenericParameterPosition == b.GenericParameterPosition;
        }

        if (a.HasElementType || b.HasElementType)
        {
            return a.HasElementType
                && b.HasElementType
                && a.IsArray == b.IsArray
                && a.IsByRef == b.IsByRef
                && a.IsPointer == b.IsPointer
                && (!a.IsArray || a.GetArrayRank() == b.GetArrayRank())
                && TypesMatch(a.GetElementType()!, b.GetElementType()!);
        }

        if (a.IsConstructedGenericType
            && b.IsConstructedGenericType
            && a.GetGenericTypeDefinition() == b.GetGenericTypeDefinition())
        {
            var argumentsA = a.GenericTypeArguments;
            var argumentsB = b.GenericTypeArguments;
            for (var i = 0; i < argumentsA.Length; i++)
            {
                if (!TypesMatch(argumentsA[i], argumentsB[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    private static int CalculateIndexerScore(ParameterInfo parameter, bool isInteger)
    {
        var paramType = parameter.ParameterType;

        if (paramType == typeof(int))
        {
            return isInteger ? 0 : 10;
        }

        if (paramType == typeof(string))
        {
            return 1;
        }

        return 5;
    }

    internal bool TryFindMemberAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes)] Type type,
        string memberName,
        BindingFlags? bindingFlags,
        PropertyInfo? indexerToTry,
        [NotNullWhen(true)] out ReflectionAccessor? accessor)
    {
        // The three lookups below reflect with binding flags the host chooses
        // (Options.Interop.ObjectWrapperReported*BindingFlags), so a trimmer cannot fold them and assumes
        // they ask for non-public members: one IL2070 each, and they are deliberately left standing.
        // Closing them means declaring NonPublicProperties | NonPublicFields | NonPublicMethods on `type`,
        // which propagates to every annotated public entry point and makes every AOT consumer preserve the
        // non-public members of every type they expose - to serve a lookup only a host that put
        // BindingFlags.NonPublic in those options can reach. The nested-type lookup at the end of this
        // method is the one that could be spelled as a constant, and is.
        //
        // look for a property, bit be wary of indexers, we don't want indexers which have name "Item" to take precedence
        PropertyInfo? property = null;
        var memberNameComparer = _memberNameComparer;
        var typeResolverMemberNameCreator = _memberNameCreator;

        PropertyInfo? GetProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type t)
        {
            foreach (var p in t.GetProperties(bindingFlags ?? PropertyBindingFlags(engine)))
            {
                if (!Filter(engine, type, p))
                {
                    continue;
                }

                // only if it's not an indexer, we can do case-ignoring matches
                var isStandardIndexer = string.Equals(p.Name, "Item", StringComparison.Ordinal) && p.GetIndexParameters().Length == 1;
                if (!isStandardIndexer)
                {
                    foreach (var name in typeResolverMemberNameCreator(p))
                    {
                        if (memberNameComparer.Equals(name, memberName))
                        {
                            // If one property hides another (e.g., by public new), the derived property is returned.
                            if (property is not null
                                && p.DeclaringType is not null
                                && property.DeclaringType is not null
                                && property.DeclaringType.IsSubclassOf(p.DeclaringType))
                            {
                                continue;
                            }
                            property = p;
                            break;
                        }
                    }
                }
            }

            return property;
        }

        property = GetProperty(type);

        if (property is null && type.IsInterface)
        {
            // check inherited interfaces
            foreach (var iface in type.GetInterfaces())
            {
                property = GetProperty(iface);
                if (property is not null)
                {
                    break;
                }
            }
        }

        // Whether a generated lane may stand in for whatever this selection lands on. The instance lane only
        // (the static one passes its own flags and registered members are never static), and only for a type
        // carrying no indexer: an indexer is probed ahead of the member itself, and a generated accessor has
        // no such probe - the same reason the shortcut in ResolvePropertyDescriptorFactory sits behind it.
        var maySubstituteGenerated = bindingFlags is null && indexerToTry is null && !JsAccessibleRegistry.IsEmpty;

        if (property is not null)
        {
            var reflected = new PropertyAccessor(property, indexerToTry);
            accessor = maySubstituteGenerated && TrySubstituteGeneratedAccessor(type, property, reflected, out var substitute)
                ? substitute
                : reflected;
            return true;
        }

        // look for a field
        FieldInfo? field = null;
        foreach (var f in type.GetFields(bindingFlags ?? FieldBindingFlags(engine)))
        {
            if (!Filter(engine, type, f))
            {
                continue;
            }

            foreach (var name in typeResolverMemberNameCreator(f))
            {
                if (memberNameComparer.Equals(name, memberName))
                {
                    field = f;
                    break;
                }
            }
        }

        if (field is not null)
        {
            var reflected = new FieldAccessor(field, indexerToTry);
            accessor = maySubstituteGenerated && TrySubstituteGeneratedAccessor(type, field, reflected, out var substitute)
                ? substitute
                : reflected;
            return true;
        }

        // if no properties were found then look for a method
        List<MethodInfo>? methods = null;
        void AddMethod(MethodInfo m, bool skipIfSignatureAlreadyPresent = false)
        {
            if (!Filter(engine, type, m))
            {
                return;
            }

            foreach (var name in typeResolverMemberNameCreator(m))
            {
                if (memberNameComparer.Equals(name, memberName))
                {
                    // an implicitly implemented interface method is also reported through the class,
                    // only add a secondary slot when it brings a new signature
                    if (skipIfSignatureAlreadyPresent && ContainsMethodWithSameSignature(methods, m))
                    {
                        return;
                    }

                    methods ??= new List<MethodInfo>();
                    methods.Add(m);
                    return;
                }
            }
        }

        var methodBindingFlags = bindingFlags ?? MethodBindingFlags(engine);

        foreach (var m in type.GetMethods(methodBindingFlags))
        {
            AddMethod(m);
        }

        foreach (var iface in type.GetInterfaces())
        {
            // Reflect the interface with the lane's own flags. A parameterless GetMethods() reports
            // public instance *and* static members whatever the caller asked for, so the static-only
            // lookup behind a TypeReference used to pick up instance interface methods. On .NET
            // Framework that is how System.Type's COM mirrors — _Type and _MemberInfo, which declare
            // an instance GetType(), InvokeMember(), GetMethod() and the rest — reached script as
            // static members of System.Type, past the static Type.GetType guard in Filter, which
            // never matched them because they are not static.
            foreach (var m in iface.GetMethods(methodBindingFlags))
            {
                AddMethod(m, skipIfSignatureAlreadyPresent: true);
            }
        }

        // TPC: need to grab the extension methods here - for overloads
        if (engine._extensionMethods.TryGetExtensionMethods(type, out var extensionMethods))
        {
            foreach (var methodInfo in extensionMethods)
            {
                AddMethod(methodInfo);
            }
        }

        // Add Object methods to interface
        if (type.IsInterface)
        {
            foreach (var m in typeof(object).GetMethods(bindingFlags ?? MethodBindingFlags(engine)))
            {
                AddMethod(m, skipIfSignatureAlreadyPresent: true);
            }
        }

        if (methods?.Count > 0)
        {
            var reflected = new MethodAccessor(type, MethodDescriptor.Build(methods));

            // A registered method is the sole candidate for its name by construction (the generator declines
            // any name carrying more than one), so a name that collected several here is one the host's name
            // policy merged and reflection has to bind.
            accessor = maySubstituteGenerated
                       && methods.Count == 1
                       && TrySubstituteGeneratedAccessor(type, methods[0], reflected, out var substitute)
                ? substitute
                : reflected;
            return true;
        }

        // Look for a nested type. Spelled as a constant rather than as `bindingFlags ?? …`, and the two are
        // the same lookup: Type.GetNestedType reads only the Public/NonPublic pair, so the one caller that
        // passes flags (ResolveStaticAccessor, Public | Static | FlattenHierarchy) asks this exactly what the
        // default asks, and neither ever carries NonPublic. Written dynamically it was a Type.Get* call whose
        // flags a trimmer cannot fold, so it demanded NonPublicNestedTypes of every annotated caller — a
        // requirement no lookup here can reach. As a constant it demands PublicNestedTypes, which is what
        // this method's `type` parameter declares, so the requirement is stated exactly where it is read.
        // It deliberately stops at TypeReference.ReferenceType rather than reaching the public entry points;
        // InteropHelper.DefaultDynamicallyAccessedMemberTypes says what carrying it there would cost.
        const BindingFlags NestedTypeBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;
        var nestedType = type.GetNestedType(memberName, NestedTypeBindingFlags);
        if (nestedType != null && Filter(engine, type, nestedType))
        {
            var typeReference = TypeReference.CreateTypeReference(engine, nestedType);
            accessor = new NestedTypeAccessor(typeReference);
            return true;
        }

        accessor = default;
        return false;
    }

    private sealed class DefaultMemberNameComparer : StringComparer
    {
        public static readonly StringComparer Instance = new DefaultMemberNameComparer();

        public override int Compare(string? x, string? y)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            var equals = false;
            if (x.Length > 0)
            {
                equals = char.ToLowerInvariant(x[0]) == char.ToLowerInvariant(y[0]);
            }

            if (equals && x.Length > 1)
            {
                equals = x.AsSpan(1).SequenceEqual(y.AsSpan(1));
            }

            return equals;
        }

        /// <summary>
        /// Consistent with <see cref="Equals(string, string)"/>: two names that differ only in the casing of
        /// their first character hash the same, because that is exactly the difference this comparer ignores.
        /// It threw <see cref="NotImplementedException"/> for as long as the comparer was used only for linear
        /// scans; the generated member tables key a dictionary with it, so it has to answer.
        /// </summary>
        public override int GetHashCode(string obj)
        {
            if (obj is null || obj.Length == 0)
            {
                return 0;
            }

            var hash = 17 * 31 + char.ToLowerInvariant(obj[0]);
            for (var i = 1; i < obj.Length; i++)
            {
                hash = hash * 31 + obj[i];
            }

            return hash;
        }
    }
}

/// <summary>
/// Key into the accessor cache a <see cref="TypeResolver"/> shares between the engines using it.
/// </summary>
/// <remarks>
/// Two independent things partition this cache. <see cref="Requirement"/> is what member resolution is
/// filtered by, so a resolution that had to skip a member for not being readable/writable must not answer a
/// lookup carrying a different requirement. <see cref="Profile"/> is the interop configuration that steers
/// resolution but lives on the engine's options rather than on the resolver, so an entry is only served back
/// to an engine that would have resolved the member the same way.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct AccessorCacheKey(
    Type Type,
    Key PropertyName,
    MemberResolutionRequirement Requirement,
    InteropResolutionProfile Profile);

[StructLayout(LayoutKind.Auto)]
internal readonly record struct StaticAccessorCacheKey(
    Type Type,
    string PropertyName,
    InteropResolutionProfile Profile);

/// <summary>
/// The interop configuration that steers member resolution but does not live on the
/// <see cref="TypeResolver"/> itself, so that its accessor cache can be partitioned by it: an entry is only
/// ever served back to an engine that would have resolved the member the same way.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is affine to an engine or a realm — the flags are values and the extension method lookup holds
/// only <see cref="MethodInfo"/>s and is itself meant to be shared.
/// </para>
/// <para>
/// A host-installed <see cref="ClrTypeConverter"/> is deliberately <b>not</b> here. It was, as "is it the stock
/// one", which gave every engine with a converter a partition of its own and so cost it the whole shared cache
/// — for one question: whether a member name converts to an index key, which resolution asks only of a type
/// carrying a non-<see cref="int"/> indexer. <see cref="TypeResolver.IsConverterNeutral"/> excludes the members
/// of exactly those types, on the way in and on the way out, so an engine with a converter shares every other
/// type with its stock siblings.
/// </para>
/// <para>
/// Hand-written rather than a positional record struct so the hash is computed once, when an engine captures
/// its profile, instead of on every cache probe.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly struct InteropResolutionProfile : IEquatable<InteropResolutionProfile>
{
    private readonly BindingFlags _fieldBindingFlags;
    private readonly BindingFlags _propertyBindingFlags;
    private readonly BindingFlags _methodBindingFlags;
    private readonly ExtensionMethodCache? _extensionMethods;
    private readonly bool _allowGetType;
    private readonly int _hashCode;

    internal InteropResolutionProfile(
        bool allowGetType,
        BindingFlags fieldBindingFlags,
        BindingFlags propertyBindingFlags,
        BindingFlags methodBindingFlags,
        ExtensionMethodCache extensionMethods)
    {
        _allowGetType = allowGetType;
        _fieldBindingFlags = fieldBindingFlags;
        _propertyBindingFlags = propertyBindingFlags;
        _methodBindingFlags = methodBindingFlags;
        _extensionMethods = extensionMethods;

        var hashCode = allowGetType ? 1 : 0;
        hashCode = (hashCode * 397) ^ (int) fieldBindingFlags;
        hashCode = (hashCode * 397) ^ (int) propertyBindingFlags;
        hashCode = (hashCode * 397) ^ (int) methodBindingFlags;
        // ExtensionMethodCache does not override GetHashCode, this is its reference identity
        _hashCode = (hashCode * 397) ^ extensionMethods.GetHashCode();
    }

    /// <summary>
    /// Whether this is a profile an engine actually captured, as opposed to the <see langword="default"/>
    /// an engine still inside its constructor carries. The extension method lookup is never null on a
    /// captured one.
    /// </summary>
    internal bool IsCaptured => _extensionMethods is not null;

    internal bool AllowGetType => _allowGetType;

    internal BindingFlags FieldBindingFlags => _fieldBindingFlags;

    internal BindingFlags PropertyBindingFlags => _propertyBindingFlags;

    internal BindingFlags MethodBindingFlags => _methodBindingFlags;

    public bool Equals(InteropResolutionProfile other)
    {
        return _hashCode == other._hashCode
               && _allowGetType == other._allowGetType
               && _fieldBindingFlags == other._fieldBindingFlags
               && _propertyBindingFlags == other._propertyBindingFlags
               && _methodBindingFlags == other._methodBindingFlags
               && ReferenceEquals(_extensionMethods, other._extensionMethods);
    }

    public override bool Equals(object? obj) => obj is InteropResolutionProfile other && Equals(other);

    public override int GetHashCode() => _hashCode;

    public static bool operator ==(InteropResolutionProfile left, InteropResolutionProfile right) => left.Equals(right);

    public static bool operator !=(InteropResolutionProfile left, InteropResolutionProfile right) => !left.Equals(right);
}
