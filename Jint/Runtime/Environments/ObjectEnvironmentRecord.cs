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
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-createmutablebinding-n-d
        /// </summary>
        public override void CreateMutableBinding(in Key name, bool configurable = true)
        {
            var propertyDescriptor = configurable
                ? new PropertyDescriptor(JsValue.Undefined, PropertyFlag.ConfigurableEnumerableWritable)
                : new PropertyDescriptor(JsValue.Undefined, PropertyFlag.NonConfigurable);

            _bindingObject.SetOwnProperty(name, propertyDescriptor);
        }

        // http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-createimmutablebinding-n-s
        public override void CreateImmutableBinding(in Key name, bool strict = false)
        {
            throw new InvalidOperationException("The concrete Environment Record method CreateImmutableBinding is never used within this specification in association with Object Environment Records.");
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-initializebinding-n-v
        /// </summary>
        public override void InitializeBinding(in Key name, JsValue value)
        {
            SetMutableBinding(name, value, false);
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
