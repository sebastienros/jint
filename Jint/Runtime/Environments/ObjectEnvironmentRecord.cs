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
        internal readonly ObjectInstance _bindingObject;
        private readonly bool _provideThis;
        private readonly bool _withEnvironment;

        public ObjectEnvironmentRecord(
            Engine engine,
            ObjectInstance bindingObject, 
            bool provideThis, 
            bool withEnvironment) : base(engine)
        {
            _bindingObject = bindingObject;
            _provideThis = provideThis;
            _withEnvironment = withEnvironment;
        }

        public override bool HasBinding(string name)
        {
            var property = new JsString(name);
            var foundBinding = HasProperty(property);

            if (!foundBinding)
            {
                return false;
            }

            if (!_withEnvironment)
            {
                return true;
            }
            
            return !IsBlocked(name);
        }

        private bool HasProperty(JsValue property)
        {
            return _bindingObject.HasProperty(property);
        }

        internal override bool TryGetBinding(
            BindingName name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            // we unwrap by name
            binding = default;

            if (!HasProperty(name.StringValue))
            {
                value = default;
                return false;
            }

            if (_withEnvironment && IsBlocked(name.StringValue))
            {
                value = default;
                return false;
            }

            var desc = _bindingObject.GetProperty(name.StringValue);
            value = ObjectInstance.UnwrapJsValue(desc, _bindingObject);
            return true;
        }

        private bool IsBlocked(JsValue property)
        {
            var unscopables = _bindingObject.Get(GlobalSymbolRegistry.Unscopables);
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
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-createmutablebinding-n-d
        /// </summary>
        public override void CreateMutableBinding(string name, bool canBeDeleted = false)
        {
            var propertyDescriptor = canBeDeleted
                ? new PropertyDescriptor(Undefined, PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding)
                : new PropertyDescriptor(Undefined, PropertyFlag.NonConfigurable | PropertyFlag.MutableBinding);

            _bindingObject.DefinePropertyOrThrow(name, propertyDescriptor);
        }
        
        /// <summary>
        ///  http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-createimmutablebinding-n-s
        /// </summary>
        public override void CreateImmutableBinding(string name, bool strict = true)
        {
            ExceptionHelper.ThrowInvalidOperationException("The concrete Environment Record method CreateImmutableBinding is never used within this specification in association with Object Environment Records.");
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-initializebinding-n-v
        /// </summary>
        public override void InitializeBinding(string name, JsValue value)
        {
            SetMutableBinding(name, value, false);
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            SetMutableBinding(new BindingName(name), value, strict);
        }

        internal override void SetMutableBinding(BindingName name, JsValue value, bool strict)
        {
            if (!_bindingObject.Set(name.StringValue, value) && strict)
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

        public override bool HasThisBinding() => false;

        public override bool HasSuperBinding() => false;

        public override JsValue WithBaseObject() => _withEnvironment ? _bindingObject : Undefined;

        
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

            return System.Array.Empty<string>();
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(_bindingObject, other);
        }

        public override JsValue GetThisBinding()
        {
            throw new System.NotImplementedException();
        }
    }
}
