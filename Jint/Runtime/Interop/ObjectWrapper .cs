using System;
using System.Linq;
using System.Reflection;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wrapps a CLR instance
    /// </summary>
    public sealed class ObjectWrapper : ObjectInstance
    {
        private readonly Object _obj;

        public ObjectWrapper(Engine engine, Object obj): base(engine)
        {
            _obj = obj;
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            PropertyDescriptor x;
            if (Properties.TryGetValue(propertyName, out x))
            {
                return x;
            }

            var type = _obj.GetType();
            var property = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == propertyName);

            if (property == null)
            {
                return PropertyDescriptor.Undefined;
            }
            else
            {
                var descriptor = new ClrDataDescriptor(Engine, property, _obj);
                Properties.Add(propertyName, descriptor);
                return descriptor;
            }
            
        }
    }
}
