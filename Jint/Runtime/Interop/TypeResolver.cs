using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Threading;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Interop strategy for resolving types and members.
    /// </summary>
    public sealed class TypeResolver
    {
        public static readonly TypeResolver Default = new TypeResolver();

        private Dictionary<ClrPropertyDescriptorFactoriesKey, ReflectionAccessor> _reflectionAccessors = new();

        /// <summary>
        /// Registers a filter that determines whether given member is wrapped to interop or returned as undefined.
        /// </summary>
        public Predicate<MemberInfo> MemberFilter { get; set; } = _ => true;

        /// <summary>
        /// Sets member name comparison strategy when finding CLR objects members.
        /// By default member's first character casing is ignored and rest of the name is compared with strict equality.
        /// </summary>
        public StringComparer MemberNameComparer { get; set; } = DefaultMemberNameComparer.Instance;

        internal ReflectionAccessor GetAccessor(Engine engine, Type type, string member, Func<ReflectionAccessor> accessorFactory = null)
        {
            var key = new ClrPropertyDescriptorFactoriesKey(type, member);

            var factories = _reflectionAccessors;
            if (factories.TryGetValue(key, out var accessor))
            {
                return accessor;
            }

            accessor = accessorFactory?.Invoke() ?? ResolvePropertyDescriptorFactory(engine, type, member);

            // racy, we don't care, worst case we'll catch up later
            Interlocked.CompareExchange(ref _reflectionAccessors,
                new Dictionary<ClrPropertyDescriptorFactoriesKey, ReflectionAccessor>(factories)
                {
                    [key] = accessor
                }, factories);

            return accessor;
        }

        private ReflectionAccessor ResolvePropertyDescriptorFactory(
            Engine engine,
            Type type,
            string memberName)
        {
            var isNumber = uint.TryParse(memberName, out _);

            // we can always check indexer if there's one, and then fall back to properties if indexer returns null
            IndexerAccessor.TryFindIndexer(engine, type, memberName, out var indexerAccessor, out var indexer);

            const BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;

            // properties and fields cannot be numbers
            if (!isNumber && TryFindMemberAccessor(type, memberName, bindingFlags, indexer, out var temp))
            {
                return temp;
            }

            if (typeof(DynamicObject).IsAssignableFrom(type))
            {
                return new DynamicObjectAccessor(type, memberName);
            }

            // if no methods are found check if target implemented indexing
            if (indexerAccessor != null)
            {
                return indexerAccessor;
            }

            // try to find a single explicit property implementation
            List<PropertyInfo> list = null;
            var typeResolverMemberNameComparer = MemberNameComparer;
            foreach (var iface in type.GetInterfaces())
            {
                foreach (var iprop in iface.GetProperties())
                {
                    if (!MemberFilter(iprop))
                    {
                        continue;
                    }

                    if (iprop.Name == "Item" && iprop.GetIndexParameters().Length == 1)
                    {
                        // never take indexers, should use the actual indexer
                        continue;
                    }

                    if (typeResolverMemberNameComparer.Equals(iprop.Name, memberName))
                    {
                        list ??= new List<PropertyInfo>();
                        list.Add(iprop);
                    }
                }
            }

            if (list?.Count == 1)
            {
                return new PropertyAccessor(memberName, list[0]);
            }

            // try to find explicit method implementations
            List<MethodInfo> explicitMethods = null;
            foreach (var iface in type.GetInterfaces())
            {
                foreach (var imethod in iface.GetMethods())
                {
                    if (!MemberFilter(imethod))
                    {
                        continue;
                    }

                    if (typeResolverMemberNameComparer.Equals(imethod.Name, memberName))
                    {
                        explicitMethods ??= new List<MethodInfo>();
                        explicitMethods.Add(imethod);
                    }
                }
            }

            if (explicitMethods?.Count > 0)
            {
                return new MethodAccessor(MethodDescriptor.Build(explicitMethods));
            }

            // try to find explicit indexer implementations
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (IndexerAccessor.TryFindIndexer(engine, interfaceType, memberName, out var accessor, out _))
                {
                    return accessor;
                }
            }

            if (engine._extensionMethods.TryGetExtensionMethods(type, out var extensionMethods))
            {
                var matches = new List<MethodInfo>();
                foreach (var method in extensionMethods)
                {
                    if (!MemberFilter(method))
                    {
                        continue;
                    }

                    if (typeResolverMemberNameComparer.Equals(method.Name, memberName))
                    {
                        matches.Add(method);
                    }
                }

                if (matches.Count > 0)
                {
                    return new MethodAccessor(MethodDescriptor.Build(matches));
                }
            }

            return ConstantValueAccessor.NullAccessor;
        }

        internal bool TryFindMemberAccessor(
            Type type,
            string memberName,
            BindingFlags bindingFlags,
            PropertyInfo indexerToTry,
            out ReflectionAccessor accessor)
        {
            // look for a property, bit be wary of indexers, we don't want indexers which have name "Item" to take precedence
            PropertyInfo property = null;
            foreach (var p in type.GetProperties(bindingFlags))
            {
                if (!MemberFilter(p))
                {
                    continue;
                }

                // only if it's not an indexer, we can do case-ignoring matches
                var isStandardIndexer = p.GetIndexParameters().Length == 1 && p.Name == "Item";
                if (!isStandardIndexer && MemberNameComparer.Equals(p.Name, memberName))
                {
                    property = p;
                    break;
                }
            }

            if (property != null)
            {
                accessor = new PropertyAccessor(memberName, property, indexerToTry);
                return true;
            }

            // look for a field
            FieldInfo field = null;
            foreach (var f in type.GetFields(bindingFlags))
            {
                if (!MemberFilter(f))
                {
                    continue;
                }

                if (MemberNameComparer.Equals(f.Name, memberName))
                {
                    field = f;
                    break;
                }
            }

            if (field != null)
            {
                accessor = new FieldAccessor(field, memberName, indexerToTry);
                return true;
            }

            // if no properties were found then look for a method
            List<MethodInfo> methods = null;
            foreach (var m in type.GetMethods(bindingFlags))
            {
                if (!MemberFilter(m))
                {
                    continue;
                }

                if (MemberNameComparer.Equals(m.Name, memberName))
                {
                    methods ??= new List<MethodInfo>();
                    methods.Add(m);
                }
            }

            if (methods?.Count > 0)
            {
                accessor = new MethodAccessor(MethodDescriptor.Build(methods));
                return true;
            }

            accessor = default;
            return false;
        }

        private sealed class DefaultMemberNameComparer : StringComparer
        {
            public static readonly StringComparer Instance = new DefaultMemberNameComparer();

            public override int Compare(string x, string y)
            {
                throw new NotImplementedException();
            }

            public override bool Equals(string x, string y)
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
#if NETSTANDARD2_1
                    equals = x.AsSpan(1).SequenceEqual(y.AsSpan(1));
#else
                    equals = x.Substring(1) == y.Substring(1);
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
}