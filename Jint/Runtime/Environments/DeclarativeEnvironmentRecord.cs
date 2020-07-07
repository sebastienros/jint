using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a declarative environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.1
    /// </summary>
    internal class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        internal readonly HybridDictionary<Binding> _dictionary = new HybridDictionary<Binding>();
        internal bool _hasBindings;

        public DeclarativeEnvironmentRecord(Engine engine) : base(engine)
        {
        }

        public sealed override bool HasBinding(string name)
        {
            return _dictionary.ContainsKey(name);
        }

        internal sealed override bool TryGetBinding(
            in BindingName name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            binding = default;
            var success = _dictionary.TryGetValue(name.Key, out binding);
            value = success && binding.IsInitialized() ? UnwrapBindingValue(strict, binding) : default;
            return success;
        }

        internal void CreateMutableBindingAndInitialize(Key name, bool canBeDeleted, JsValue value)
        {
            _hasBindings = true;
            _dictionary[name] = new Binding(value, canBeDeleted, mutable: true, strict: false);
        }

        internal void CreateImmutableBindingAndInitialize(Key name, bool strict, JsValue value)
        {
            _hasBindings = true;
            _dictionary[name] = new Binding(value, canBeDeleted: false, mutable: false, strict);
        }

        public sealed override void CreateMutableBinding(string name, bool canBeDeleted = false)
        {
            _hasBindings = true;
            _dictionary[name] = new Binding(null, canBeDeleted, mutable: true, strict: false);
        }
        
        public sealed override void CreateImmutableBinding(string name, bool strict = true)
        {
            _hasBindings = true;
            _dictionary[name] = new Binding(null, canBeDeleted: false, mutable: false, strict);
        }

        public sealed override void InitializeBinding(string name, JsValue value)
        {
            _hasBindings = true;
            _dictionary.TryGetValue(name, out var binding);
            _dictionary[name] = binding.ChangeValue(value);
        }

        internal override void SetMutableBinding(in BindingName name, JsValue value, bool strict)
        {
            SetMutableBinding(name.Key.Name, value, strict);
        }

        public sealed override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            var key = (Key) name;
            if (!_dictionary.TryGetValue(key, out var binding))
            {
                if (strict)
                {
                    ExceptionHelper.ThrowReferenceError(_engine, key);
                }
                CreateMutableBindingAndInitialize(key, canBeDeleted: true, value);
                return;
            }

            if (binding.Strict)
            {
                strict = true;
            }

            // Is it an uninitialized binding?
            if (!binding.IsInitialized())
            {
                ExceptionHelper.ThrowReferenceError<object>(_engine, message: "Cannot access '" +  key + "' before initialization");
            }
            
            if (binding.Mutable)
            {
                _hasBindings = true;
                _dictionary[key] = binding.ChangeValue(value);
            }
            else
            {
                if (strict)
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Assignment to constant variable.");
                }
            }
        }

        public sealed override JsValue GetBindingValue(string name, bool strict)
        {
            _dictionary.TryGetValue(name, out var binding);
            return UnwrapBindingValue(strict, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue UnwrapBindingValue(bool strict, in Binding binding)
        {
            if (!binding.Mutable && !binding.IsInitialized())
            {
                if (strict)
                {
                    ThrowUninitializedBindingException();
                }

                return Undefined;
            }

            return binding.Value;
        }

        private void ThrowUninitializedBindingException()
        {
            throw new JavaScriptException(_engine.ReferenceError, "Can't access an uninitialized immutable binding.");
        }

        public sealed override bool DeleteBinding(string name)
        {
            if (!_dictionary.TryGetValue(name, out var binding))
            {
                return true;
            }

            if (!binding.CanBeDeleted)
            {
                return false;
            }

            _dictionary.Remove(name);
            _hasBindings = _dictionary.Count > 0;

            return true;
        }
        
        public override bool HasThisBinding() => false;

        public override bool HasSuperBinding() => false;

        public override JsValue WithBaseObject() => Undefined;

        public sealed override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        /// <inheritdoc />
        internal sealed override string[] GetAllBindingNames()
        {
            if (_dictionary is null)
            {
                return System.Array.Empty<string>();
            }

            var keys = new string[_dictionary.Count];
            var n = 0;
            foreach (var entry in _dictionary)
            {
                keys[n++] = entry.Key;
            }

            return keys;
        }

        public override JsValue GetThisBinding()
        {
            throw new System.NotImplementedException();
        }
    }
}
