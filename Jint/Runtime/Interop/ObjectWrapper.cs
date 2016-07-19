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

        internal TypeInteropDescriptor TypeDescriptor { get; set; }

        public ObjectWrapper(Engine engine, Object obj)
            : base(engine)
        {
            Target = obj;

            TypeDescriptor = obj != null ? engine.GetTypeInteropDescriptor(obj.GetType()) : null;
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
            var descriptor = TypeDescriptor.GetProperty(Engine, propertyName);

            var sharedDescriptor = descriptor as ISharedDescriptor;
            if (sharedDescriptor != null)
            {
                sharedDescriptor.SetTarget(Target);
            }

            return descriptor;
        }
    }
}
