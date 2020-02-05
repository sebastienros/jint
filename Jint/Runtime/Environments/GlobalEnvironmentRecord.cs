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
        private readonly GlobalObject _global;

        public GlobalEnvironmentRecord(Engine engine, GlobalObject global) : base(engine)
        {
            _global = global;
        }

        public override bool HasBinding(string name)
        {
            var foundBinding = _global.HasProperty(name);
            if (!foundBinding)
            {
                return false;
            }

            // TODO If the withEnvironment flag of envRec is false, return true.

            return !IsBlocked(name);
        }

        internal override bool TryGetBinding(
            string name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            // we unwrap by name
            binding = default;

            Key key = name;
            var desc = _global.GetProperty(key);
            if (desc == PropertyDescriptor.Undefined || IsBlocked(name))
            {
                value = default;
                return false;
            }

            value = ObjectInstance.UnwrapJsValue(desc, _global);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsBlocked(string property)
        {
            return _global._symbols != null && CheckSymbolsForBlocked(property);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool CheckSymbolsForBlocked(string property)
        {
            var unscopables = _global.Get(GlobalSymbolRegistry.Unscopables);
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

            _global.DefinePropertyOrThrow(name, propertyDescriptor);
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            if (!_global.Set(name, value) && strict)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            var desc = _global.GetProperty(name);
            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceError(_engine, name.ToString());
            }

            return ObjectInstance.UnwrapJsValue(desc, this);
        }

        public override bool DeleteBinding(string name)
        {
            return _global.Delete(name);
        }

        public override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        internal override string[] GetAllBindingNames()
        {
            return _global.GetOwnProperties().Select( x=> x.Key.ToString()).ToArray();
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(_global, other);
        }
    }
}
