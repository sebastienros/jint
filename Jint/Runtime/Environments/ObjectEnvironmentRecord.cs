using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents an object environment record
    /// https://tc39.es/ecma262/#sec-object-environment-records
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

        internal override bool HasBinding(BindingName name)
        {
            var foundBinding = HasProperty(name.Value);

            if (!foundBinding)
            {
                return false;
            }

            if (!_withEnvironment)
            {
                return true;
            }

            return !IsBlocked(name.Value);
        }

        private bool HasProperty(JsValue property)
        {
            return _bindingObject.HasProperty(property);
        }

        internal override bool TryGetBinding(
            BindingName name,
            bool strict,
            out Binding binding,
            [NotNullWhen(true)] out JsValue? value)
        {
            // we unwrap by name
            binding = default;

            if (!HasProperty(name.Value))
            {
                value = default;
                return false;
            }

            if (_withEnvironment && IsBlocked(name.Value))
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
            _bindingObject.DefinePropertyOrThrow(name, new PropertyDescriptor(Undefined, canBeDeleted
                ? PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding
                : PropertyFlag.NonConfigurable | PropertyFlag.MutableBinding));
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-environment-records-createimmutablebinding-n-s
        /// </summary>
        public override void CreateImmutableBinding(string name, bool strict = true)
        {
            ExceptionHelper.ThrowInvalidOperationException("The concrete Environment Record method CreateImmutableBinding is never used within this specification in association with Object Environment Records.");
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-environment-records-initializebinding-n-v
        /// </summary>
        public override void InitializeBinding(string name, JsValue value)
        {
            SetMutableBinding(name, value, false);
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            var jsString = new JsString(name);
            if (strict && !_bindingObject.HasProperty(jsString))
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
            }

            _bindingObject.Set(jsString, value);
        }

        internal override void SetMutableBinding(BindingName name, JsValue value, bool strict)
        {
            if (strict && !_bindingObject.HasProperty(name.Value))
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name.Key);
            }

            _bindingObject.Set(name.Value, value);
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            var desc = _bindingObject.GetProperty(name);
            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
            }

            return ObjectInstance.UnwrapJsValue(desc, _bindingObject);
        }

        public override bool DeleteBinding(string name)
        {
            return _bindingObject.Delete(name);
        }

        public override bool HasThisBinding() => false;

        public override bool HasSuperBinding() => false;

        public override JsValue WithBaseObject() => _withEnvironment ? _bindingObject : Undefined;

        public sealed override bool HasBindings()
        {
            return _bindingObject._properties?.Count > 0;
        }

        internal override string[] GetAllBindingNames()
        {
            if (!ReferenceEquals(_bindingObject, null))
            {
                var names = new List<string>(_bindingObject._properties?.Count ?? 0);
                foreach (var name in _bindingObject.GetOwnProperties())
                {
                    names.Add(name.Key.ToString());
                }
                return names.ToArray();
            }

            return Array.Empty<string>();
        }

        public override bool Equals(JsValue? other)
        {
            return ReferenceEquals(_bindingObject, other);
        }

        public override JsValue GetThisBinding()
        {
            throw new NotImplementedException();
        }
    }
}
