using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop
{
	/// <summary>
	/// Wraps a CLR instance
	/// </summary>
	public sealed class ObjectWrapper : ObjectInstance, IObjectWrapper
    {
        public ObjectWrapper(Engine engine, object obj)
            : base(engine)
        {
            Target = obj;
            var type = obj.GetType();
            if (ObjectIsArrayLikeClrCollection(type))
            {
                // create a forwarder to produce length from Count or Length if one of them is present
                var lengthProperty = type.GetProperty("Count") ?? type.GetProperty("Length");
                if (lengthProperty is null)
                {
                    return;
                }
                IsArrayLike = true;

                var functionInstance = new ClrFunctionInstance(engine, "length", (thisObj, arguments) => JsNumber.Create((int) lengthProperty.GetValue(obj)));
                var descriptor = new GetSetPropertyDescriptor(functionInstance, Undefined, PropertyFlag.Configurable);
                SetProperty(KnownKeys.Length, descriptor);
            }
        }

        private static bool ObjectIsArrayLikeClrCollection(Type type)
        {
            if (typeof(ICollection).IsAssignableFrom(type))
            {
                return true;
            }
            
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (!interfaceType.IsGenericType)
                {
                    continue;
                }
                
                if (interfaceType.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)
                    || interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    return true;
                }
            }

            return false;
        }

        public object Target { get; }

        public override bool IsArrayLike { get; }

        public override bool Set(JsValue property, JsValue value, JsValue receiver)
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
            if (Target is IDictionary dictionary && includeStrings)
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
                var iteratorFunction = new ClrFunctionInstance(Engine, "iterator", (thisObject, arguments) => _engine.Iterator.Construct(this), 1, PropertyFlag.Configurable);
                var iteratorProperty = new PropertyDescriptor(iteratorFunction, PropertyFlag.Configurable | PropertyFlag.Writable);
                SetProperty(GlobalSymbolRegistry.Iterator, iteratorProperty);
                return iteratorProperty;
            }

            var memberAccessor = Engine.Options._MemberAccessor;

            if (memberAccessor != null)
            {
                var result = memberAccessor.Invoke(Engine, Target, property.ToString());

                if (result != null)
                {
                    return new PropertyDescriptor(result, PropertyFlag.OnlyEnumerable);
                }
            }

            var type = Target.GetType();
            var key = new ClrPropertyDescriptorFactoriesKey(type, property.ToString());

            if (!_engine.ClrPropertyDescriptorFactories.TryGetValue(key, out var factory))
            {
                factory = ResolveProperty(type, property.ToString());
                _engine.ClrPropertyDescriptorFactories[key] = factory;
            }

            var descriptor = factory(_engine, Target);
            AddProperty(property, descriptor);
            return descriptor;
        }
        
        private Func<Engine, object, PropertyDescriptor> ResolveProperty(Type type, string propertyName)
        {
            var isNumber = uint.TryParse(propertyName, out _);

            // properties and fields cannot be numbers
            if (!isNumber)
            {
                // look for a property, bit be wary of indexers, we don't want indexers which have name "Item" to take precedence
                PropertyInfo property = null;
                foreach (var p in type.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    // only if it's not an indexer, we can do case-ignoring matches
                    var isStandardIndexer = p.GetIndexParameters().Length == 1 && p.Name == "Item";
                    if (!isStandardIndexer && EqualsIgnoreCasing(p.Name, propertyName))
                    {
                        property = p;
                        break;
                    }
                }

                if (property != null)
                {
                    return (engine, target) => new PropertyInfoDescriptor(engine, property, target);
                }

                // look for a field
                FieldInfo field = null;
                foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    if (EqualsIgnoreCasing(f.Name, propertyName))
                    {
                        field = f;
                        break;
                    }
                }

                if (field != null)
                {
                    return (engine, target) => new FieldInfoDescriptor(engine, field, target);
                }
                
                // if no properties were found then look for a method
                List<MethodInfo> methods = null;
                foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    if (EqualsIgnoreCasing(m.Name, propertyName))
                    {
                        methods ??= new List<MethodInfo>();
                        methods.Add(m);
                    }
                }

                if (methods?.Count > 0)
                {
                    return (engine, target) => new PropertyDescriptor(new MethodInfoFunctionInstance(engine, methods.ToArray()), PropertyFlag.OnlyEnumerable);
                }

            }

            // if no methods are found check if target implemented indexing
            if (IndexDescriptor.TryFindIndexer(_engine, type, propertyName, out _, out _, out _))
            {
                return (engine, target) => new IndexDescriptor(engine, propertyName, target);
            }

            // try to find a single explicit property implementation
            List<PropertyInfo> list = null;
            foreach (Type iface in type.GetInterfaces())
            {
                foreach (var iprop in iface.GetProperties())
                {
                    if (EqualsIgnoreCasing(iprop.Name, propertyName))
                    {
                        list ??= new List<PropertyInfo>();
                        list.Add(iprop);
                    }
                }
            }

            if (list?.Count == 1)
            {
                return (engine, target) => new PropertyInfoDescriptor(engine, list[0], target);
            }

            // try to find explicit method implementations
            List<MethodInfo> explicitMethods = null;
            foreach (Type iface in type.GetInterfaces())
            {
                foreach (var imethod in iface.GetMethods())
                {
                    if (EqualsIgnoreCasing(imethod.Name, propertyName))
                    {
                        explicitMethods ??= new List<MethodInfo>();
                        explicitMethods.Add(imethod);
                    }
                }
            }

            if (explicitMethods?.Count > 0)
            {
                return (engine, target) => new PropertyDescriptor(new MethodInfoFunctionInstance(engine, explicitMethods.ToArray()), PropertyFlag.OnlyEnumerable);
            }

            // try to find explicit indexer implementations
            foreach (Type interfaceType in type.GetInterfaces())
            {
                if (IndexDescriptor.TryFindIndexer(_engine, interfaceType, propertyName, out _, out _, out _))
                {
                    return (engine, target) => new IndexDescriptor(engine, interfaceType, propertyName, target);
                }
            }

            return (engine, target) => PropertyDescriptor.Undefined;
        }

        private static bool EqualsIgnoreCasing(string s1, string s2)
        {
            bool equals = false;
            if (s1.Length == s2.Length)
            {
                if (s1.Length > 0)
                {
                    equals = char.ToLowerInvariant(s1[0]) == char.ToLowerInvariant(s2[0]);
                }
                if (equals && s1.Length > 1)
                {
                    equals = s1.Substring(1) == s2.Substring(1);
                }
            }
            return equals;
        }
    }
}
