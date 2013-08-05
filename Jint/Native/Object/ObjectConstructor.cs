using System;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public class ObjectConstructor : FunctionInstance
    {
        public ObjectConstructor(ObjectInstance prototype) : base(prototype, null, null)
        {
            prototype.DefineOwnProperty("hasOwnProperty", new DataDescriptor(new BuiltInPropertyWrapper((Func<ObjectInstance, string, bool>)HasOwnProperty, prototype)), false);
            prototype.DefineOwnProperty("toString", new DataDescriptor(new BuiltInPropertyWrapper((Func<ObjectInstance, string>)ToString, prototype)), false);
        }

        public override dynamic Call(Engine interpreter, object thisObject, dynamic[] arguments)
        {
            return Undefined.Instance;
        }

        public ObjectInstance Construct(dynamic[] arguments)
        {
            var instance = new ObjectInstance(this.Prototype);

            // the constructor is the function constructor of an object
            instance.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            instance.DefineOwnProperty("prototype", new DataDescriptor(this.Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);
            return instance;
        }

        private static bool HasOwnProperty(ObjectInstance thisObject, string propertyName)
        {
            var desc = thisObject.GetOwnProperty(propertyName);
            return desc != Undefined.Instance;
        }

        private static string ToString(ObjectInstance thisObject)
        {
            if (thisObject == null || thisObject == Undefined.Instance)
            {
                return "[object Undefined]";
            }

            if (thisObject == Null.Instance)
            {
                return "[object Null]";
            }

            return string.Format("[object {0}]", thisObject.Class);
        
        }

    }
}
