using System.Linq;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Optimized for GlobalObject, which we know of and can skip some virtual calls.
    /// http://www.ecma-international.org/ecma-262/6.0/#sec-global-environment-records
    /// </summary>
    internal sealed class GlobalEnvironmentRecord : EnvironmentRecord
    {
        private readonly GlobalObject _bindingObject;

        public GlobalEnvironmentRecord(Engine engine, GlobalObject bindingObject) : base(engine)
        {
            _bindingObject = bindingObject;
        }

        public override bool HasBinding(string name)
        {
            var foundBinding = _bindingObject.HasProperty(name);
            if (!foundBinding)
            {
                return false;
            }

            // TODO If the withEnvironment flag of envRec is false, return true.

            if (IsBlocked(name))
            {
                return false;
            }

            return true;
        }

        internal override bool TryGetBinding(
            string name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            // we unwrap by name
            binding = default;

            if (!_bindingObject.HasProperty(name) || IsBlocked(name))
            {
                value = default;
                return false;
            }

            var desc = _bindingObject.GetProperty(name);
            value = ObjectInstance.UnwrapJsValue(desc, _bindingObject);
            return true;
        }

        private bool IsBlocked(string property)
        {
            return _bindingObject._symbols != null && CheckSymbolsForBlocked(property);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool CheckSymbolsForBlocked(string property)
        {
            var unscopables = _bindingObject.Get(GlobalSymbolRegistry.Unscopables);
            if (unscopables is ObjectInstance oi)
            {
                var blocked = TypeConverter.ToBoolean(oi.Get(new JsString(property)));
                if (blocked)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2.2
        /// </summary>
        public override void CreateMutableBinding(string name, JsValue value, bool configurable = false)
        {
            var propertyDescriptor = configurable
                ? new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable)
                : new PropertyDescriptor(value, PropertyFlag.NonConfigurable);

            _bindingObject.DefinePropertyOrThrow(name, propertyDescriptor);
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            if (!_bindingObject.Set(name, value) && strict)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            var desc = _bindingObject.GetProperty(name);
            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceError(_engine, name.ToString());
            }

            return ObjectInstance.UnwrapJsValue(desc, this);
        }

        public override bool DeleteBinding(string name)
        {
            return _bindingObject.Delete(name);
        }

        public override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        internal override string[] GetAllBindingNames()
        {
            return _bindingObject.GetOwnProperties().Select( x=> x.Key.ToString()).ToArray();
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(_bindingObject, other);
        }
    }
}
