using System;
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents an object environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2
    /// </summary>
    internal sealed class ObjectEnvironmentRecord : EnvironmentRecord
    {
        private readonly ObjectInstance _bindingObject;
        private bool _canUseFastPropertyChecks;
        private readonly bool _provideThis;

        public ObjectEnvironmentRecord(Engine engine, ObjectInstance bindingObject, bool provideThis) : base(engine)
        {
            _bindingObject = bindingObject;
            _canUseFastPropertyChecks = _bindingObject.GetType() == typeof(ObjectInstance);
            _provideThis = provideThis;
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
            in KeyValue name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            // we unwrap by name
            binding = default;

            if (!_bindingObject.HasProperty(name.Value) || IsBlocked(name.Value))
            {
                value = default;
                return false;
            }

            var desc = _bindingObject.GetProperty(name.Value);
            value = ObjectInstance.UnwrapJsValue(desc, _bindingObject);
            return true;
        }

        private bool IsBlocked(JsValue property)
        {
            var unscopables = _bindingObject._symbols != null
                ? _bindingObject.Get(GlobalSymbolRegistry.Unscopables)
                : null;

            if (unscopables is ObjectInstance oi)
            {
                var blocked = TypeConverter.ToBoolean(oi.Get(property));
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
        public override void CreateMutableBinding(JsValue name, JsValue value, bool configurable = false)
        {
            var propertyDescriptor = configurable
                ? new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable)
                : new PropertyDescriptor(value, PropertyFlag.NonConfigurable);

            _bindingObject.DefinePropertyOrThrow(name, propertyDescriptor);
        }

        public override void SetMutableBinding(JsValue name, JsValue value, bool strict)
        {
            if (!_bindingObject.Set(name, value) && strict)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }
        }

        public override JsValue GetBindingValue(JsValue name, bool strict)
        {
            var desc = _bindingObject.GetProperty(name);
            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceError(_engine, name.ToString());
            }

            return ObjectInstance.UnwrapJsValue(desc, this);
        }

        public override bool DeleteBinding(JsValue name)
        {
            return _bindingObject.Delete(name);
        }

        public override JsValue ImplicitThisValue()
        {
            if (_provideThis)
            {
                return _bindingObject;
            }

            return Undefined;
        }

        internal override string[] GetAllBindingNames()
        {
            if (!ReferenceEquals(_bindingObject, null))
            {
                return _bindingObject.GetOwnProperties().Select( x=> x.Key.ToString()).ToArray();
            }

            return ArrayExt.Empty<string>();
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(_bindingObject, other);
        }
    }
}
