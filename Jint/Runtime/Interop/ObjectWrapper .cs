using System;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wrapps a CLR instance
    /// </summary>
    public sealed class ObjectWrapper : ObjectInstance, IObjectWrapper
    {
        public Object Target { get; set; }

        public ObjectWrapper(Engine engine, Object obj): base(engine)
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
            {
                return x;
            }

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
                return new PropertyDescriptor(new MethodInfoFunctionInstance(Engine, methods), false, true, false);
            }

            return PropertyDescriptor.Undefined;
        }
    }
}
