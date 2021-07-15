using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop
{
	/// <summary>
	/// Wraps a CLR instance
	/// </summary>
	public sealed class ObjectWrapper : ObjectInstance, IObjectWrapper, IEquatable<ObjectWrapper>
    {
        private readonly TypeDescriptor _typeDescriptor;

        public ObjectWrapper(Engine engine, object obj)
            : base(engine)
        {
            Target = obj;
            _typeDescriptor = TypeDescriptor.Get(obj.GetType());
            if (_typeDescriptor.IsArrayLike)
            {
                // create a forwarder to produce length from Count or Length if one of them is present
                var lengthProperty = obj.GetType().GetProperty("Count") ?? obj.GetType().GetProperty("Length");
                if (lengthProperty is null)
                {
                    return;
                }
                var functionInstance = new ClrFunctionInstance(engine, "length", (_, _) => JsNumber.Create((int) lengthProperty.GetValue(obj)));
                var descriptor = new GetSetPropertyDescriptor(functionInstance, Undefined, PropertyFlag.Configurable);
                SetProperty(KnownKeys.Length, descriptor);
            }
        }


        public object Target { get; }

        public override bool IsArrayLike => _typeDescriptor.IsArrayLike;

        internal override bool HasOriginalIterator => IsArrayLike;

        internal override bool IsIntegerIndexedArray => _typeDescriptor.IsIntegerIndexedArray;

        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            // check if we can take shortcuts for empty object, no need to generate properties
            if (property is JsString stringKey)
            {
                var member = stringKey.ToString();
                if (_properties is null || !_properties.ContainsKey(member))
                {
                    // can try utilize fast path
                    var accessor = GetAccessor(_engine, Target.GetType(), member);

                    // CanPut logic
                    if (!accessor.Writable || !_engine.Options.Interop.AllowWrite)
                    {
                        return false;
                    }

                    accessor.SetValue(_engine, Target, value);
                    return true;
                }
            }

            return SetSlow(property, value);
        }

        private bool SetSlow(JsValue property, JsValue value)
        {
            if (!CanPut(property))
            {
                return false;
            }

            var ownDesc = GetOwnProperty(property);

            if (ownDesc == null)
            {
                return false;
            }

            ownDesc.Value = value;
            return true;
        }

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            if (property.IsInteger() && Target is IList list)
            {
                var index = (int) ((JsNumber) property)._value;
                return (uint) index < list.Count ? FromObject(_engine, list[index]) : Undefined;
            }

            if (property.IsSymbol() && property != GlobalSymbolRegistry.Iterator)
            {
                // wrapped objects cannot have symbol properties
                return Undefined;
            }

            if (property is JsString stringKey)
            {
                var member = stringKey.ToString();
                var result = Engine.Options.Interop.MemberAccessor(Engine, Target, member);
                if (result is not null)
                {
                    return result;
                }

                if (_properties is null || !_properties.ContainsKey(member))
                {
                    // can try utilize fast path
                    var accessor = GetAccessor(_engine, Target.GetType(), member);
                    var value = accessor.GetValue(_engine, Target);
                    if (value is not null)
                    {
                        return FromObject(_engine, value);
                    }
                }
            }

            return base.Get(property, receiver);
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.None | Types.String | Types.Symbol)
        {
            return new List<JsValue>(EnumerateOwnPropertyKeys(types));
        }

        public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            foreach (var key in EnumerateOwnPropertyKeys(Types.String | Types.Symbol))
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(key, GetOwnProperty(key));
            }
        }

        private IEnumerable<JsValue> EnumerateOwnPropertyKeys(Types types)
        {
            var basePropertyKeys = base.GetOwnPropertyKeys(types);
            // prefer object order, add possible other properties after
            var processed = basePropertyKeys.Count > 0 ? new HashSet<JsValue>() : null;

            var includeStrings = (types & Types.String) != 0;
            if (includeStrings && Target is IDictionary<string, object> stringKeyedDictionary) // expando object for instance
            {
                foreach (var key in stringKeyedDictionary.Keys)
                {
                    var jsString = JsString.Create(key);
                    processed?.Add(jsString);
                    yield return jsString;
                }
            }
            else if (includeStrings && Target is IDictionary dictionary)
            {
                // we take values exposed as dictionary keys only
                foreach (var key in dictionary.Keys)
                {
                    if (_engine.ClrTypeConverter.TryConvert(key, typeof(string), CultureInfo.InvariantCulture, out var stringKey))
                    {
                        var jsString = JsString.Create((string) stringKey);
                        processed?.Add(jsString);
                        yield return jsString;
                    }
                }
            }
            else if (includeStrings)
            {
                // we take public properties and fields
                var type = Target.GetType();
                foreach (var p in type.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    var indexParameters = p.GetIndexParameters();
                    if (indexParameters.Length == 0)
                    {
                        var jsString = JsString.Create(p.Name);
                        processed?.Add(jsString);
                        yield return jsString;
                    }
                }

                foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    var jsString = JsString.Create(f.Name);
                    processed?.Add(jsString);
                    yield return jsString;
                }
            }

            if (processed != null)
            {
                // we have base keys
                for (var i = 0; i < basePropertyKeys.Count; i++)
                {
                    var key = basePropertyKeys[i];
                    if (processed.Add(key))
                    {
                        yield return key;
                    }
                }
            }
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (TryGetProperty(property, out var x))
            {
                return x;
            }

            if (property.IsSymbol() && property == GlobalSymbolRegistry.Iterator)
            {
                var iteratorFunction = new ClrFunctionInstance(
                    Engine, "iterator",
                    (thisObject, arguments) => _engine.Realm.Intrinsics.Iterator.Construct(this, intrinsics => intrinsics.Iterator.GenericIteratorPrototypeObject),
                    1,
                    PropertyFlag.Configurable);
                var iteratorProperty = new PropertyDescriptor(iteratorFunction, PropertyFlag.Configurable | PropertyFlag.Writable);
                SetProperty(GlobalSymbolRegistry.Iterator, iteratorProperty);
                return iteratorProperty;
            }

            var member = property.ToString();
            var result = Engine.Options.Interop.MemberAccessor(Engine, Target, member);
            if (result is not null)
            {
                return new PropertyDescriptor(result, PropertyFlag.OnlyEnumerable);
            }

            var accessor = GetAccessor(_engine, Target.GetType(), member);
            var descriptor = accessor.CreatePropertyDescriptor(_engine, Target);
            SetProperty(member, descriptor);
            return descriptor;
        }

        // need to be public for advanced cases like RavenDB yielding properties from CLR objects
        public static PropertyDescriptor GetPropertyDescriptor(Engine engine, object target, MemberInfo member)
        {
            // fast path which uses slow search if not found for some reason
            ReflectionAccessor Factory()
            {
                return member switch
                {
                    PropertyInfo pi => new PropertyAccessor(pi.Name, pi),
                    MethodBase mb => new MethodAccessor(MethodDescriptor.Build(new[] {mb})),
                    FieldInfo fi => new FieldAccessor(fi),
                    _ => null
                };
            }
            return GetAccessor(engine, target.GetType(), member.Name, Factory).CreatePropertyDescriptor(engine, target);
        }

        private static ReflectionAccessor GetAccessor(Engine engine, Type type, string member, Func<ReflectionAccessor> accessorFactory = null)
        {
            var key = new ClrPropertyDescriptorFactoriesKey(type, member);

            var factories = Engine.ReflectionAccessors;
            if (factories.TryGetValue(key, out var accessor))
            {
                return accessor;
            }

            accessor = accessorFactory?.Invoke() ?? ResolvePropertyDescriptorFactory(engine, type, member);

            // racy, we don't care, worst case we'll catch up later
            Interlocked.CompareExchange(ref Engine.ReflectionAccessors,
                new Dictionary<ClrPropertyDescriptorFactoriesKey, ReflectionAccessor>(factories)
                {
                    [key] = accessor
                }, factories);

            return accessor;
        }

        private static ReflectionAccessor ResolvePropertyDescriptorFactory(Engine engine, Type type, string memberName)
        {
            var isNumber = uint.TryParse(memberName, out _);

            // we can always check indexer if there's one, and then fall back to properties if indexer returns null
            IndexerAccessor.TryFindIndexer(engine, type, memberName, out var indexerAccessor, out var indexer);

            // properties and fields cannot be numbers
            if (!isNumber && TryFindStringPropertyAccessor(type, memberName, indexer, out var temp))
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
            foreach (Type iface in type.GetInterfaces())
            {
                foreach (var iprop in iface.GetProperties())
                {
                    if (iprop.Name == "Item" && iprop.GetIndexParameters().Length == 1)
                    {
                        // never take indexers, should use the actual indexer
                        continue;
                    }

                    if (EqualsIgnoreCasing(iprop.Name, memberName))
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
            foreach (Type iface in type.GetInterfaces())
            {
                foreach (var imethod in iface.GetMethods())
                {
                    if (EqualsIgnoreCasing(imethod.Name, memberName))
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
                    if (EqualsIgnoreCasing(method.Name, memberName))
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

        private static bool TryFindStringPropertyAccessor(
            Type type,
            string memberName,
            PropertyInfo indexerToTry,
            out ReflectionAccessor wrapper)
        {
            // look for a property, bit be wary of indexers, we don't want indexers which have name "Item" to take precedence
            PropertyInfo property = null;
            foreach (var p in type.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
            {
                // only if it's not an indexer, we can do case-ignoring matches
                var isStandardIndexer = p.GetIndexParameters().Length == 1 && p.Name == "Item";
                if (!isStandardIndexer && EqualsIgnoreCasing(p.Name, memberName))
                {
                    property = p;
                    break;
                }
            }

            if (property != null)
            {
                wrapper = new PropertyAccessor(memberName, property, indexerToTry);
                return true;
            }

            // look for a field
            FieldInfo field = null;
            foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
            {
                if (EqualsIgnoreCasing(f.Name, memberName))
                {
                    field = f;
                    break;
                }
            }

            if (field != null)
            {
                wrapper = new FieldAccessor(field, memberName, indexerToTry);
                return true;
            }

            // if no properties were found then look for a method
            List<MethodInfo> methods = null;
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
            {
                if (EqualsIgnoreCasing(m.Name, memberName))
                {
                    methods ??= new List<MethodInfo>();
                    methods.Add(m);
                }
            }

            if (methods?.Count > 0)
            {
                wrapper = new MethodAccessor(MethodDescriptor.Build(methods));
                return true;
            }

            wrapper = default;
            return false;
        }

        private static bool EqualsIgnoreCasing(string s1, string s2)
        {
            if (s1.Length != s2.Length)
            {
                return false;
            }

            var equals = false;
            if (s1.Length > 0)
            {
                equals = char.ToLowerInvariant(s1[0]) == char.ToLowerInvariant(s2[0]);
            }

            if (@equals && s1.Length > 1)
            {
#if NETSTANDARD2_1
                equals = s1.AsSpan(1).SequenceEqual(s2.AsSpan(1));
#else
                equals = s1.Substring(1) == s2.Substring(1);
#endif
            }
            return equals;
        }

        public override bool Equals(JsValue obj)
        {
            return Equals(obj as ObjectWrapper);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ObjectWrapper);
        }

        public bool Equals(ObjectWrapper other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(Target, other.Target);
        }

        public override int GetHashCode()
        {
            return Target?.GetHashCode() ?? 0;
        }
    }
}
