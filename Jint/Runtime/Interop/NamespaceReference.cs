using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Any instance on this class represents a reference to a CLR namespace.
    /// Accessing its properties will look for a class of the full name, or instantiate
    /// a new <see cref="NamespaceReference"/> as it assumes that the property is a deeper
    /// level of the current namespace
    /// </summary>
    public class NamespaceReference : ObjectInstance
    {
        private readonly string _path;

        public NamespaceReference(Engine engine, string path) : base(engine)
        {
            _path = path;
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            if (throwOnError)
            {
                throw new JavaScriptException(Engine.TypeError, "Can't define a property of a NamespaceReference");
            }

            return false;
        }

        public override bool Delete(string propertyName, bool throwOnError)
        {
            if (throwOnError)
            {
                throw new JavaScriptException(Engine.TypeError, "Can't delete a property of a NamespaceReference");
            }

            return false;
        }

        public override JsValue Get(string propertyName)
        {
            var newPath = _path + "." + propertyName;
            var type = Type.GetType(newPath);
            if (type != null)
            {
                return TypeReference.CreateTypeReference(Engine, type);
            }

            // the new path doesn't represent a known class, thus return a new namespace instance

            return new NamespaceReference(Engine, newPath);
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            return PropertyDescriptor.Undefined;
        }

        public override string ToString()
        {
            return "[Namespace: " + _path + "]";
        }
    }
}
