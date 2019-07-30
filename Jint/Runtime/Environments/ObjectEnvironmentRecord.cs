using System;
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents an object environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2
    /// </summary>
    public sealed class ObjectEnvironmentRecord : EnvironmentRecord
    {
        private readonly ObjectInstance _bindingObject;
        private readonly bool _provideThis;

        public ObjectEnvironmentRecord(Engine engine, ObjectInstance bindingObject, bool provideThis) : base(engine)
        {
            _bindingObject = bindingObject;
            _provideThis = provideThis;
        }

        public override bool HasBinding(in Key name)
        {
            return _bindingObject.HasProperty(name);
        }

        internal override bool TryGetBinding(
            in Key name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            // we unwrap by name
            binding = default;

            var desc = _bindingObject.GetProperty(name);
            if (desc == PropertyDescriptor.Undefined)
            {
                value = default;
                return false;
            }

            value = ObjectInstance.UnwrapJsValue(desc, this);
            return true;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2.2
        /// </summary>
        public override void CreateMutableBinding(in Key name, JsValue value, bool configurable = true)
        {
            var propertyDescriptor = configurable
                ? new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable)
                : new PropertyDescriptor(value, PropertyFlag.NonConfigurable);

            _bindingObject.SetOwnProperty(name, propertyDescriptor);
        }

        public override void SetMutableBinding(in Key name, JsValue value, bool strict)
        {
            _bindingObject.Put(name, value, strict);
        }

        public override JsValue GetBindingValue(in Key name, bool strict)
        {
            var desc = _bindingObject.GetProperty(name);
            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceError(_engine, name);
            }

            return ObjectInstance.UnwrapJsValue(desc, this);
        }

        public override bool DeleteBinding(in Key name)
        {
            return _bindingObject.Delete(name, false);
        }

        public override JsValue ImplicitThisValue()
        {
            if (_provideThis)
            {
                return _bindingObject;
            }

            return Undefined;
        }

        public override string[] GetAllBindingNames()
        {
            if (!ReferenceEquals(_bindingObject, null))
            {
                return _bindingObject.GetOwnProperties().Select( x=> x.Key).ToArray();
            }

            return ArrayExt.Empty<string>();
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(_bindingObject, other);
        }

        internal override void FunctionWasCalled()
        {
        }
    }
}
