using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
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
                AddProperty(CommonProperties.Length, descriptor);
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

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property.IsSymbol())
            {
                return PropertyDescriptor.Undefined;
            }

            if (TryGetProperty(property, out var x))
            {
                return x;
            }

            var type = Target.GetType();
            var key = new Engine.ClrPropertyDescriptorFactoriesKey(type, property.ToString());

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
                // look for a property
                PropertyInfo property = null;
                foreach (var p in type.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    if (EqualsIgnoreCasing(p.Name, propertyName, _engine.Options._IsIgnoredCasingResolveProperty))
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
                    if (EqualsIgnoreCasing(f.Name, propertyName, _engine.Options._IsIgnoredCasingResolveProperty))
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
                    if (EqualsIgnoreCasing(m.Name, propertyName, _engine.Options._IsIgnoredCasingResolveProperty))
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
                    if (EqualsIgnoreCasing(iprop.Name, propertyName, _engine.Options._IsIgnoredCasingResolveProperty))
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
                    if (EqualsIgnoreCasing(imethod.Name, propertyName, _engine.Options._IsIgnoredCasingResolveProperty))
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

        private static bool EqualsIgnoreCasing(string s1, string s2, bool ignoreCasing = true)
        {
            bool equals = false;
            if (s1.Length == s2.Length)
            {
                if (!ignoreCasing)
                    return (s1 == s2);

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
