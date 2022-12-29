using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a declarative environment record
    /// https://tc39.es/ecma262/#sec-declarative-environment-records
    /// </summary>
    internal class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        internal HybridDictionary<Binding>? _dictionary;
        internal readonly bool _catchEnvironment;

        public DeclarativeEnvironmentRecord(Engine engine, bool catchEnvironment = false) : base(engine)
        {
            _catchEnvironment = catchEnvironment;
        }

        public sealed override bool HasBinding(string name)
        {
            return _dictionary is not null && _dictionary.ContainsKey(name);
        }

        internal sealed override bool HasBinding(BindingName name)
        {
            return _dictionary is not null &&_dictionary.ContainsKey(name.Key);
        }

        internal override bool TryGetBinding(
            BindingName name,
            bool strict,
            out Binding binding,
            [NotNullWhen(true)] out JsValue? value)
        {
            binding = default;
            var success = _dictionary is not null &&_dictionary.TryGetValue(name.Key, out binding);
            value = success && binding.IsInitialized() ? binding.Value : default;
            return success;
        }

        internal void CreateMutableBindingAndInitialize(Key name, bool canBeDeleted, JsValue value)
        {
            _dictionary ??= new HybridDictionary<Binding>();
            _dictionary[name] = new Binding(value, canBeDeleted, mutable: true, strict: false);
        }

        internal void CreateImmutableBindingAndInitialize(Key name, bool strict, JsValue value)
        {
            _dictionary ??= new HybridDictionary<Binding>();
            _dictionary[name] = new Binding(value, canBeDeleted: false, mutable: false, strict);
        }

        public sealed override void CreateMutableBinding(string name, bool canBeDeleted = false)
        {
            _dictionary ??= new HybridDictionary<Binding>();
            _dictionary[name] = new Binding(null!, canBeDeleted, mutable: true, strict: false);
        }

        public sealed override void CreateImmutableBinding(string name, bool strict = true)
        {
            _dictionary ??= new HybridDictionary<Binding>();
            _dictionary[name] = new Binding(null!, canBeDeleted: false, mutable: false, strict);
        }

        public sealed override void InitializeBinding(string name, JsValue value)
        {
            _dictionary ??= new HybridDictionary<Binding>();
            _dictionary.SetOrUpdateValue(name, static (current, value) => current.ChangeValue(value), value);
        }

        internal sealed override void SetMutableBinding(BindingName name, JsValue value, bool strict)
        {
            SetMutableBinding(name.Key.Name, value, strict);
        }

        public sealed override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            var key = (Key) name;
            if (_dictionary is null || !_dictionary.TryGetValue(key, out var binding))
            {
                if (strict)
                {
                    ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
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
                ThrowUninitializedBindingError(name);
            }

            if (binding.Mutable)
            {
                _dictionary[key] = binding.ChangeValue(value);
            }
            else
            {
                if (strict)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, "Assignment to constant variable.");
                }
            }
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            if (_dictionary is not null && _dictionary.TryGetValue(name, out var binding) && binding.IsInitialized())
            {
                return binding.Value;
            }

            ThrowUninitializedBindingError(name);
            return null!;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowUninitializedBindingError(string name)
        {
            ExceptionHelper.ThrowReferenceError(_engine.Realm, $"Cannot access '{name}' before initialization");
        }

        public sealed override bool DeleteBinding(string name)
        {
            if (_dictionary is null || !_dictionary.TryGetValue(name, out var binding))
            {
                return true;
            }

            if (!binding.CanBeDeleted)
            {
                return false;
            }

            _dictionary.Remove(name);

            return true;
        }

        public override bool HasThisBinding() => false;

        public override bool HasSuperBinding() => false;

        public sealed override JsValue WithBaseObject() => Undefined;

        public sealed override bool HasBindings()
        {
            return _dictionary?.Count > 0;
        }

        /// <inheritdoc />
        internal sealed override string[] GetAllBindingNames()
        {
            if (_dictionary is null)
            {
                return Array.Empty<string>();
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
            return Undefined;
        }

        public void Clear()
        {
            _dictionary = null;
        }
    }
}
