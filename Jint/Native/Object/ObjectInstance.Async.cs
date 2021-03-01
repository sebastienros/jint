using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Jint.Native.Object
{
    public partial class ObjectInstance : JsValue, IEquatable<ObjectInstance>
    {
        public override Task<JsValue> GetAsync(JsValue property, JsValue receiver)
        {
            var desc = GetProperty(property);
            return UnwrapJsValueAsync(desc, receiver);
        }

        internal async static Task<JsValue> UnwrapJsValueAsync(PropertyDescriptor desc, JsValue thisObject)
        {
            var value = (desc._flags & PropertyFlag.CustomJsValue) != 0
                ? desc.CustomValue
                : desc._value;

            // IsDataDescriptor inlined
            if ((desc._flags & (PropertyFlag.WritableSet | PropertyFlag.Writable)) != 0
                || !ReferenceEquals(value, null))
            {
                return value ?? Undefined;
            }

            return await UnwrapFromGetterAsync(desc, thisObject);
        }

        /// <summary>
        /// A rarer case.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private async static Task<JsValue> UnwrapFromGetterAsync(PropertyDescriptor desc, JsValue thisObject)
        {
            var getter = desc.Get ?? Undefined;
            if (getter.IsUndefined())
            {
                return Undefined;
            }

            var functionInstance = (FunctionInstance)getter;
            return await functionInstance._engine.CallAsync(functionInstance, thisObject, Arguments.Empty, expression: null);
        }
    }
}