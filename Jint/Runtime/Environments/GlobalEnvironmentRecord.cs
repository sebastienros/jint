using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-global-environment-records
    /// </summary>
    public sealed class GlobalEnvironmentRecord : EnvironmentRecord
    {
        internal readonly ObjectInstance _global;

        // we expect it to be GlobalObject, but need to allow to something host-defined, like Window
        private readonly GlobalObject? _globalObject;

        // Environment records are needed by debugger
        internal readonly DeclarativeEnvironmentRecord _declarativeRecord;
        private readonly HashSet<string> _varNames = new();

        public GlobalEnvironmentRecord(Engine engine, ObjectInstance global) : base(engine)
        {
            _global = global;
            _globalObject = global as GlobalObject;
            _declarativeRecord = new DeclarativeEnvironmentRecord(engine);
        }

        public ObjectInstance GlobalThisValue => _global;

        public override bool HasBinding(string name)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                return true;
            }

            if (_globalObject is not null)
            {
                return _globalObject.HasProperty(name);
            }

            return _global.HasProperty(new JsString(name));
        }

        internal override bool HasBinding(BindingName name)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                return true;
            }

            if (_globalObject is not null)
            {
                return _globalObject.HasProperty(name.Key);
            }

            return _global.HasProperty(name.Value);
        }

        internal override bool TryGetBinding(
            BindingName name,
            bool strict,
            out Binding binding,
            [NotNullWhen(true)] out JsValue? value)
        {
            if (_declarativeRecord.TryGetBinding(name, strict, out binding, out value))
            {
                return true;
            }

            // we unwrap by name
            binding = default;
            value = default;

            // normal case is to find
            if (_global._properties!._dictionary.TryGetValue(name.Key, out var property)
                && property != PropertyDescriptor.Undefined)
            {
                value = ObjectInstance.UnwrapJsValue(property, _global);
                return true;
            }

            if (_global._prototype is not null)
            {
                return TryGetBindingForGlobalParent(name, out value);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetBindingForGlobalParent(
            BindingName name,
            [NotNullWhen(true)] out JsValue? value)
        {
            value = default;

            var parent = _global._prototype!;
            var property = parent.GetOwnProperty(name.Value);

            if (property == PropertyDescriptor.Undefined)
            {
                return false;
            }

            value = ObjectInstance.UnwrapJsValue(property, _global);
            return true;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-global-environment-records-createmutablebinding-n-d
        /// </summary>
        public override void CreateMutableBinding(string name, bool canBeDeleted = false)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                ThrowAlreadyDeclaredException(name);
            }

            _declarativeRecord.CreateMutableBinding(name, canBeDeleted);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-global-environment-records-createimmutablebinding-n-s
        /// </summary>
        public override void CreateImmutableBinding(string name, bool strict = true)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                ThrowAlreadyDeclaredException(name);
            }

            _declarativeRecord.CreateImmutableBinding(name, strict);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowAlreadyDeclaredException(string name)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, name + " has already been declared");
        }

        public override void InitializeBinding(string name, JsValue value)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                _declarativeRecord.InitializeBinding(name, value);
            }
            else
            {
                _global._properties![name].Value = value;
            }
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                _declarativeRecord.SetMutableBinding(name, value, strict);
            }
            else
            {
                if (_globalObject is not null)
                {
                    // fast inlined path as we know we target global
                    if (!_globalObject.SetFromMutableBinding(name, value, strict) && strict)
                    {
                        ExceptionHelper.ThrowTypeError(_engine.Realm);
                    }
                }
                else
                {
                    SetMutableBindingUnlikely(name, value, strict);
                }
            }
        }

        internal override void SetMutableBinding(BindingName name, JsValue value, bool strict)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                _declarativeRecord.SetMutableBinding(name, value, strict);
            }
            else
            {
                if (_globalObject is not null)
                {
                    // fast inlined path as we know we target global
                    if (!_globalObject.SetFromMutableBinding(name.Key, value, strict) && strict)
                    {
                        ExceptionHelper.ThrowTypeError(_engine.Realm);
                    }
                }
                else
                {
                    SetMutableBindingUnlikely(name.Key.Name, value, strict);
                }
            }
        }

        private void SetMutableBindingUnlikely(string name, JsValue value, bool strict)
        {
            // see ObjectEnvironmentRecord.SetMutableBinding
            var jsString = new JsString(name);
            if (strict && !_global.HasProperty(jsString))
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
            }

            _global.Set(jsString, value);
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                return _declarativeRecord.GetBindingValue(name, strict);
            }

            // see ObjectEnvironmentRecord.GetBindingValue
            var desc = PropertyDescriptor.Undefined;
            if (_globalObject is not null)
            {
                if (_globalObject._properties?.TryGetValue(name, out desc) == false)
                {
                    desc = PropertyDescriptor.Undefined;
                }
            }
            else
            {
                desc = _global.GetProperty(name);
            }

            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
            }

            return ObjectInstance.UnwrapJsValue(desc, _global);
        }

        public override bool DeleteBinding(string name)
        {
            if (_declarativeRecord.HasBinding(name))
            {
                return _declarativeRecord.DeleteBinding(name);
            }

            if (_global.HasOwnProperty(name))
            {
                var status = _global.Delete(name);
                if (status)
                {
                    _varNames.Remove(name);
                }

                return status;
            }

            return true;
        }

        public override bool HasThisBinding()
        {
            return true;
        }

        public override bool HasSuperBinding()
        {
            return false;
        }

        public override JsValue WithBaseObject()
        {
            return Undefined;
        }

        public override JsValue GetThisBinding()
        {
            return _global;
        }

        public bool HasVarDeclaration(string name)
        {
            return _varNames.Contains(name);
        }

        public bool HasLexicalDeclaration(string name)
        {
            return _declarativeRecord.HasBinding(name);
        }

        public bool HasRestrictedGlobalProperty(string name)
        {
            if (_globalObject is not null)
            {
                return _globalObject._properties?.TryGetValue(name, out var desc) == true
                       && !desc.Configurable;
            }

            var existingProp = _global.GetOwnProperty(name);
            if (existingProp == PropertyDescriptor.Undefined)
            {
                return false;
            }

            return !existingProp.Configurable;
        }

        public bool CanDeclareGlobalVar(string name)
        {
            if (_global._properties!.ContainsKey(name))
            {
                return true;
            }

            return _global.Extensible;
        }

        public bool CanDeclareGlobalFunction(string name)
        {
            if (!_global._properties!.TryGetValue(name, out var existingProp)
                || existingProp == PropertyDescriptor.Undefined)
            {
                return _global.Extensible;
            }

            if (existingProp.Configurable)
            {
                return true;
            }

            if (existingProp.IsDataDescriptor() && existingProp.Writable && existingProp.Enumerable)
            {
                return true;
            }

            return false;
        }

        public void CreateGlobalVarBinding(string name, bool canBeDeleted)
        {
            Key key = name;
            if (!_global._properties!.ContainsKey(key) && _global.Extensible)
            {
                _global._properties[key] = new PropertyDescriptor(Undefined, canBeDeleted
                    ? PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding
                    : PropertyFlag.NonConfigurable | PropertyFlag.MutableBinding);
            }

            _varNames.Add(name);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createglobalfunctionbinding
        /// </summary>
        public void CreateGlobalFunctionBinding(string name, JsValue value, bool canBeDeleted)
        {
            var jsString = new JsString(name);
            var existingProp = _global.GetOwnProperty(jsString);

            PropertyDescriptor desc;
            if (existingProp == PropertyDescriptor.Undefined || existingProp.Configurable)
            {
                desc = new PropertyDescriptor(value, true, true, canBeDeleted);
            }
            else
            {
                desc = new PropertyDescriptor(value, PropertyFlag.None);
            }

            _global.DefinePropertyOrThrow(jsString, desc);
            _global.Set(jsString, value, false);
            _varNames.Add(name);
        }

        public sealed override bool HasBindings()
        {
            return _declarativeRecord.HasBindings() || _globalObject?._properties?.Count > 0 || _global._properties?.Count > 0;
        }

        internal override string[] GetAllBindingNames()
        {
            // JT: Rather than introduce a new method for the debugger, I'm reusing this one,
            // which - in spite of the very general name - is actually only used by the debugger
            // at this point.
            var names = new List<string>(_global._properties?.Count ?? 0 + _declarativeRecord._dictionary?.Count ?? 0);
            foreach (var name in _global.GetOwnProperties())
            {
                names.Add(name.Key.ToString());
            }

            foreach (var name in _declarativeRecord.GetAllBindingNames())
            {
                names.Add(name);
            }

            return names.ToArray();
        }

        public override bool Equals(JsValue? other)
        {
            return ReferenceEquals(this, other);
        }
    }
}
