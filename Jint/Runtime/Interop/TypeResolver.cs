using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Jint.Runtime.Interop.Reflection;

#pragma warning disable IL2067
#pragma warning disable IL2070
#pragma warning disable IL2072
#pragma warning disable IL2075

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
    /// How many accessors this resolver currently holds. The cache never evicts, so this is the retention
    /// the resolver commits to: it must stay bounded by the distinct members the engines using it resolve,
    /// and must not grow with the number of engines constructed.
    /// </summary>
    internal int ResolvedAccessorCount => _reflectionAccessors.Count + _staticAccessors.Count;

    private Predicate<MemberInfo> _memberFilter = static _ => true;
    private Func<MemberInfo, IEnumerable<string>> _memberNameCreator = NameCreator;
    private StringComparer _memberNameComparer = DefaultMemberNameComparer.Instance;

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
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type type,
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

        var profile = engine._interopResolutionProfile;
        if (!profile.IsCaptured)
        {
            // The engine is still running its configuration callbacks and has not captured the profile that
            // partitions the cache yet — a host-installed ITypeConverter, for one, is only in place once they
            // have all run. Resolve without touching the cache rather than risk mislabeling an entry.
            return ResolvePropertyDescriptorFactory(engine, type, member, requirement, throwOnError);
        }

        var key = new AccessorCacheKey(type, member, requirement, profile);

        var factories = _reflectionAccessors;
        if (factories.TryGetValue(key, out var accessor))
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

        if (IsShareable(engine, accessor))
        {
            // racy, we don't care: both racers resolved the same member the same way
            factories.TryAdd(key, accessor);
        }

        return accessor;
    }

    internal ReflectionAccessor GetStaticAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes | DynamicallyAccessedMemberTypes.Interfaces)]
        Type type,
        string member)
    {
        var profile = engine._interopResolutionProfile;
        if (!profile.IsCaptured)
        {
            return ResolveStaticAccessor(engine, type, member);
        }

        var key = new StaticAccessorCacheKey(type, member, profile);
        if (_staticAccessors.TryGetValue(key, out var accessor))
        {
            return accessor;
        }

        accessor = ResolveStaticAccessor(engine, type, member);
        if (IsShareable(engine, accessor))
        {
            _staticAccessors.TryAdd(key, accessor);
        }

        return accessor;
    }

    internal MethodDescriptor[] GetConstructors(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        return _constructors.GetOrAdd(
            type,
            t =>
            {
                List<ConstructorInfo> constructors = [.. t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)];
                constructors.RemoveAll(x => !Filter(engine, t, x));
                return MethodDescriptor.Build(constructors);
            });
    }

    private ReflectionAccessor ResolveStaticAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes | DynamicallyAccessedMemberTypes.Interfaces)]
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
    private static bool IsShareable(Engine engine, ReflectionAccessor accessor)
    {
        // A nested type resolves to a TypeReference, which is a JsValue owned by the engine that created it:
        // sharing it would hand one engine's object to another and pin that engine for the resolver's lifetime.
        if (accessor is NestedTypeAccessor)
        {
            return false;
        }

        // An indexer accessor bakes in the index key that the engine's ITypeConverter produced from the member
        // name. The stock converter only ever yields a plain CLR value, but a host-installed one may return
        // anything at all, including something bound to its engine.
        if (accessor is IndexerAccessor && !engine._typeConverterIsDefault)
        {
            return false;
        }

        return true;
    }

    private ReflectionAccessor ResolvePropertyDescriptorFactory(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        string memberName,
        MemberResolutionRequirement requirement,
        bool throwOnError)
    {
        var isInteger = long.TryParse(memberName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

        // we can always check indexer if there's one, and then fall back to properties if indexer returns null
        IndexerAccessor.TryFindIndexer(engine, type, memberName, out var indexerAccessor, out var indexer);

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
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        string memberName,
        BindingFlags? bindingFlags,
        PropertyInfo? indexerToTry,
        [NotNullWhen(true)] out ReflectionAccessor? accessor)
    {
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

        if (property is not null)
        {
            accessor = new PropertyAccessor(property, indexerToTry);
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
            accessor = new FieldAccessor(field, indexerToTry);
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
            accessor = new MethodAccessor(type, MethodDescriptor.Build(methods));
            return true;
        }

        // look for nested type
        var nestedType = type.GetNestedType(memberName, bindingFlags ?? BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
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

        public override int GetHashCode(string obj)
        {
            throw new NotImplementedException();
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
/// Nothing here is affine to an engine or a realm — the flags are values, the extension method lookup holds
/// only <see cref="MethodInfo"/>s and is itself meant to be shared, and a host-installed
/// <see cref="ITypeConverter"/> is reduced to "is it the stock one", never captured, because those are
/// routinely constructed per engine.
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
    private readonly bool _stockTypeConverter;
    private readonly int _hashCode;

    internal InteropResolutionProfile(
        bool allowGetType,
        BindingFlags fieldBindingFlags,
        BindingFlags propertyBindingFlags,
        BindingFlags methodBindingFlags,
        ExtensionMethodCache extensionMethods,
        bool stockTypeConverter)
    {
        _allowGetType = allowGetType;
        _fieldBindingFlags = fieldBindingFlags;
        _propertyBindingFlags = propertyBindingFlags;
        _methodBindingFlags = methodBindingFlags;
        _extensionMethods = extensionMethods;
        _stockTypeConverter = stockTypeConverter;

        var hashCode = allowGetType ? 1 : 0;
        hashCode = (hashCode * 397) ^ (int) fieldBindingFlags;
        hashCode = (hashCode * 397) ^ (int) propertyBindingFlags;
        hashCode = (hashCode * 397) ^ (int) methodBindingFlags;
        // ExtensionMethodCache does not override GetHashCode, this is its reference identity
        hashCode = (hashCode * 397) ^ extensionMethods.GetHashCode();
        _hashCode = (hashCode * 397) ^ (stockTypeConverter ? 1 : 0);
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
               && _stockTypeConverter == other._stockTypeConverter
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
