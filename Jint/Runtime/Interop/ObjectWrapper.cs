using System;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using System.Collections;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a CLR instance
    /// </summary>
    public sealed class ObjectWrapper : ObjectInstance, IObjectWrapper
    {
        public Object Target { get; set; }

        public ObjectWrapper(Engine engine, Object obj)
            : base(engine)
        {
            Target = obj;
        }

        public override void Put(string propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc == null)
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError, "Unknown member: " + propertyName);
                }
                else
                {
                    return;
                }
            }

            ownDesc.Value = value;
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            PropertyDescriptor x;
            if (Properties.TryGetValue(propertyName, out x))
                return x;

            var type = Target.GetType();

            // look for a property
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

            if (property != null)
            {
                var descriptor = new PropertyInfoDescriptor(Engine, property, Target);
                Properties.Add(propertyName, descriptor);
                return descriptor;
            }

            // look for a field
            var field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public);

            if (field != null)
            {
                var descriptor = new FieldInfoDescriptor(Engine, field, Target);
                Properties.Add(propertyName, descriptor);
                return descriptor;
            }

            // if no properties were found then look for a method 
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == propertyName)
                .ToArray();

            if (methods.Any())
            {
                var descriptor = new PropertyDescriptor(new MethodInfoFunctionInstance(Engine, methods), false, true, false);
                Properties.Add(propertyName, descriptor);
                return descriptor;
            }

            // if no methods are found check if target implemented indexing
            if (type.GetProperties().Where(p => p.GetIndexParameters().Length != 0).FirstOrDefault() != null)
            {
                return new IndexDescriptor(Engine, propertyName, Target);
            }

            var interfaces = type.GetInterfaces();

            // try to find a single explicit property implementation
            var explicitProperties = (from iface in interfaces
                                      from iprop in iface.GetProperties()
                                      where propertyName.Equals(iprop.Name)
                                      select iprop).ToArray();

            if (explicitProperties.Length == 1)
            {
                var descriptor = new PropertyInfoDescriptor(Engine, explicitProperties[0], Target);
                Properties.Add(propertyName, descriptor);
                return descriptor;
            }

            // try to find explicit method implementations
            var explicitMethods = (from iface in interfaces
                                   from imethod in iface.GetMethods()
                                   where propertyName.Equals(imethod.Name)
                                   select imethod).ToArray();

            if (explicitMethods.Length > 0)
            {
                var descriptor = new PropertyDescriptor(new MethodInfoFunctionInstance(Engine, explicitMethods), false, true, false);
                Properties.Add(propertyName, descriptor);
                return descriptor;
            }

            // try to find explicit indexer implementations
            var explicitIndexers =
                (from iface in interfaces
                 from iprop in iface.GetProperties()
                 where iprop.GetIndexParameters().Length != 0
                 select iprop).ToArray();

            if (explicitIndexers.Length == 1)
            {
                return new IndexDescriptor(Engine, explicitIndexers[0].DeclaringType, propertyName, Target);
            }

            return PropertyDescriptor.Undefined;
        }
    }
}
