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
    internal sealed class ObjectEnvironment : Environment
    {
        internal readonly ObjectInstance _bindingObject;
        private readonly bool _provideThis;
        private readonly bool _withEnvironment;

        public ObjectEnvironment(
            Engine engine,
            ObjectInstance bindingObject,
            bool provideThis,
            bool withEnvironment) : base(engine)
        {
            _bindingObject = bindingObject;
            _provideThis = provideThis;
            _withEnvironment = withEnvironment;
        }

        internal override bool HasBinding(Key name)
        {
            var property = new JsString(name.Name);
            var foundBinding = HasProperty(property);

            if (!foundBinding)
            {
                return false;
            }

            if (!_withEnvironment)
            {
                return true;
            }

            return !IsBlocked(property);
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
        internal override void CreateMutableBinding(Key name, bool canBeDeleted = false)
        {
            _bindingObject.DefinePropertyOrThrow(name.Name, new PropertyDescriptor(Undefined, canBeDeleted
                ? PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding
                : PropertyFlag.NonConfigurable | PropertyFlag.MutableBinding));
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-environment-records-createimmutablebinding-n-s
        /// </summary>
        internal override void CreateImmutableBinding(Key name, bool strict = true)
        {
            ExceptionHelper.ThrowInvalidOperationException("The concrete Environment Record method CreateImmutableBinding is never used within this specification in association with Object Environment Records.");
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-environment-records-initializebinding-n-v
        /// </summary>
        internal override void InitializeBinding(Key name, JsValue value) => SetMutableBinding(name, value, strict: false);

        internal override void SetMutableBinding(Key name, JsValue value, bool strict)
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

        internal override JsValue GetBindingValue(Key name, bool strict)
        {
            var desc = _bindingObject.GetProperty(name.Name);
            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
            }

            return ObjectInstance.UnwrapJsValue(desc, _bindingObject);
        }

        internal override bool DeleteBinding(Key name) => _bindingObject.Delete(name.Name);

        internal override bool HasThisBinding() => false;

        internal override bool HasSuperBinding() => false;

        internal override JsValue WithBaseObject() => _withEnvironment ? _bindingObject : Undefined;

        internal override bool HasBindings() => _bindingObject._properties?.Count > 0;

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

        internal override JsValue GetThisBinding()
        {
            throw new NotImplementedException();
        }
    }
}
