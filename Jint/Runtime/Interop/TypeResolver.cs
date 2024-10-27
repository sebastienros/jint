using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Jint.Runtime.Interop.Reflection;

#pragma warning disable IL2067
#pragma warning disable IL2070
#pragma warning disable IL2072
#pragma warning disable IL2075

namespace Jint.Runtime.Interop;

/// <summary>
/// Interop strategy for resolving types and members.
/// </summary>
public sealed class TypeResolver
{
    public static readonly TypeResolver Default = new();

    /// <summary>
    /// Registers a filter that determines whether given member is wrapped to interop or returned as undefined.
    /// By default allows all but will also be limited by <see cref="Options.InteropOptions.AllowGetType"/> configuration.
    /// </summary>
    /// <seealso cref="Options.InteropOptions.AllowGetType"/>
    public Predicate<MemberInfo> MemberFilter { get; set; } = static _ => true;

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

        return (engine.Options.Interop.AllowGetType || !string.Equals(m.Name, nameof(GetType), StringComparison.Ordinal)) && MemberFilter(m);
    }

    /// <summary>
    /// Gives the exposed names for a member. Allows to expose C# convention following member like IsSelected
    /// as more JS idiomatic "selected" for example. Defaults to returning the <see cref="MemberInfo.Name"/> as-is.
    /// </summary>
    public Func<MemberInfo, IEnumerable<string>> MemberNameCreator { get; set; } = NameCreator;

    private static IEnumerable<string> NameCreator(MemberInfo info)
    {
        yield return info.Name;
    }

    /// <summary>
    /// Sets member name comparison strategy when finding CLR objects members.
    /// By default member's first character casing is ignored and rest of the name is compared with strict equality.
    /// </summary>
    public StringComparer MemberNameComparer { get; set; } = DefaultMemberNameComparer.Instance;

    internal ReflectionAccessor GetAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        string member,
        bool mustBeReadable,
        bool mustBeWritable,
        Func<ReflectionAccessor?>? accessorFactory = null)
    {
        var key = new Engine.ClrPropertyDescriptorFactoriesKey(type, member);

        var factories = engine._reflectionAccessors;
        if (factories.TryGetValue(key, out var accessor))
        {
            return accessor;
        }

        accessor = accessorFactory?.Invoke() ?? ResolvePropertyDescriptorFactory(engine, type, member, mustBeReadable, mustBeWritable);

        // don't cache if numeric indexer
        if (uint.TryParse(member, out _))
        {
            return accessor;
        }

        // racy, we don't care, worst case we'll catch up later
        Interlocked.CompareExchange(ref engine._reflectionAccessors,
            new Dictionary<Engine.ClrPropertyDescriptorFactoriesKey, ReflectionAccessor>(factories)
            {
                [key] = accessor
            }, factories);

        return accessor;
    }

    private ReflectionAccessor ResolvePropertyDescriptorFactory(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        string memberName,
        bool mustBeReadable,
        bool mustBeWritable)
    {
        var isInteger = long.TryParse(memberName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

        // we can always check indexer if there's one, and then fall back to properties if indexer returns null
        IndexerAccessor.TryFindIndexer(engine, type, memberName, out var indexerAccessor, out var indexer);

        // properties and fields cannot be numbers
        if (!isInteger
            && TryFindMemberAccessor(engine, type, memberName, bindingFlags: null, indexer, out var temp)
            && (!mustBeReadable || temp.Readable)
            && (!mustBeWritable || temp.Writable))
        {
            return temp;
        }

        if (typeof(DynamicObject).IsAssignableFrom(type))
        {
            return new DynamicObjectAccessor();
        }

        var typeResolverMemberNameComparer = MemberNameComparer;
        var typeResolverMemberNameCreator = MemberNameCreator;

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
                        if (typeResolverMemberNameComparer.Equals(name, memberName))
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

        if (engine.Options.Interop.ThrowOnUnresolvedMember)
        {
            throw new MissingMemberException($"Cannot access property '{memberName}' on type '{type.FullName}");
        }

        return ConstantValueAccessor.NullAccessor;
    }

    private static int CalculateIndexerScore(ParameterInfo parameter, bool isInteger)
    {
        var paramType = parameter.ParameterType;

        if (paramType == typeof(int))
        {
            return  isInteger ? 0 : 10;
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
        var memberNameComparer = MemberNameComparer;
        var typeResolverMemberNameCreator = MemberNameCreator;

        PropertyInfo? GetProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type t)
        {
            foreach (var p in t.GetProperties(bindingFlags ?? engine.Options.Interop.ObjectWrapperReportedPropertyBindingFlags))
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
        foreach (var f in type.GetFields(bindingFlags ?? engine.Options.Interop.ObjectWrapperReportedFieldBindingFlags))
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
        void AddMethod(MethodInfo m)
        {
            if (!Filter(engine, type, m))
            {
                return;
            }

            foreach (var name in typeResolverMemberNameCreator(m))
            {
                if (memberNameComparer.Equals(name, memberName))
                {
                    methods ??= new List<MethodInfo>();
                    methods.Add(m);
                }
            }
        }

        foreach (var m in type.GetMethods(bindingFlags ?? engine.Options.Interop.ObjectWrapperReportedMethodBindingFlags))
        {
            AddMethod(m);
        }

        foreach (var iface in type.GetInterfaces())
        {
            foreach (var m in iface.GetMethods())
            {
                AddMethod(m);
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
            foreach (var m in typeof(object).GetMethods(bindingFlags ?? engine.Options.Interop.ObjectWrapperReportedMethodBindingFlags))
            {
                AddMethod(m);
            }
        }

        if (methods?.Count > 0)
        {
            accessor = new MethodAccessor(type, MethodDescriptor.Build(methods));
            return true;
        }

        // look for nested type
        var nestedType = type.GetNestedType(memberName, bindingFlags ?? BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
        if (nestedType != null)
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
#if SUPPORTS_SPAN_PARSE
                    equals = x.AsSpan(1).SequenceEqual(y.AsSpan(1));
#else
                equals = string.Equals(x.Substring(1), y.Substring(1), StringComparison.Ordinal);
#endif
            }

            return equals;
        }

        public override int GetHashCode(string obj)
        {
            throw new NotImplementedException();
        }
    }
}
