using System.Runtime.ExceptionServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a Clr method into a FunctionInstance
    /// </summary>
    public sealed class ClrFunctionInstance : FunctionInstance, IEquatable<ClrFunctionInstance>
    {
        internal readonly Func<JsValue, JsValue[], JsValue> _func;

        public ClrFunctionInstance(
            Engine engine,
            string name,
            Func<JsValue, JsValue[], JsValue> func,
            int length = 0,
            PropertyFlag lengthFlags = PropertyFlag.AllForbidden)
            : base(engine, engine.Realm, new JsString(name))
        {
            _func = func;

            _prototype = engine._originalIntrinsics.Function.PrototypeObject;

            _length = lengthFlags == PropertyFlag.AllForbidden
                ? PropertyDescriptor.AllForbiddenDescriptor.ForNumber(length)
                : new PropertyDescriptor(JsNumber.Create(length), lengthFlags);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            try
            {
                return _func(thisObject, arguments);
            }
            catch (Exception e) when (e is not JavaScriptException)
            {
                if (_engine.Options.Interop.ExceptionHandler(e))
                {
                    ExceptionHelper.ThrowJavaScriptException(_realm.Intrinsics.Error, e.Message);
                }
                else
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }

                return Undefined;
            }
        }

        public override bool Equals(JsValue? obj)
        {
            return Equals(obj as ClrFunctionInstance);
        }

        public bool Equals(ClrFunctionInstance? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_func == other._func)
            {
                return true;
            }

            return false;
        }
    }
}
