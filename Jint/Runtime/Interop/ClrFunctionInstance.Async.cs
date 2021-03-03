using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;
using System;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a Clr method into a FunctionInstance
    /// </summary>
    public sealed partial class ClrFunctionInstance : FunctionInstance, IEquatable<ClrFunctionInstance>
    {
        internal readonly Func<JsValue, JsValue[], Task<JsValue>> _funcAsync;

        public ClrFunctionInstance(
            Engine engine,
            string name,
            Func<JsValue, JsValue[], Task<JsValue>> funcAsync,
            int length = 0,
            PropertyFlag lengthFlags = PropertyFlag.AllForbidden)
            : base(engine, !string.IsNullOrWhiteSpace(name) ? new JsString(name) : null)
        {
            _name = name;
            _funcAsync = funcAsync;

            _prototype = engine.Function.PrototypeObject;

            _length = lengthFlags == PropertyFlag.AllForbidden
                ? PropertyDescriptor.AllForbiddenDescriptor.ForNumber(length)
                : new PropertyDescriptor(JsNumber.Create(length), lengthFlags);
        }

        public async override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
        {
            if (_funcAsync != null) return await _funcAsync(thisObject, arguments);
            else return await CallAsync(thisObject, arguments);
        }
    }
}