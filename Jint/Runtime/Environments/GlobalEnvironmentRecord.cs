using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/6.0/#sec-global-environment-records
    /// </summary>
    internal sealed class GlobalEnvironmentRecord : EnvironmentRecord
    {
        private readonly DeclarativeEnvironmentRecord _declarativeRecord;
        private readonly ObjectEnvironmentRecord _objectRecord;
        private readonly HashSet<string> _varNames = new HashSet<string>();

        public GlobalEnvironmentRecord(Engine engine, GlobalObject global) : base(engine)
        {
            _objectRecord = new ObjectEnvironmentRecord(engine, global, provideThis: false, withEnvironment: false);
            _declarativeRecord = new DeclarativeEnvironmentRecord(engine);
        }

        public ObjectInstance GlobalThisValue => _objectRecord._bindingObject;

        public override bool HasBinding(string name)
        {
            return (_declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name)) || _objectRecord.HasBinding(name);
        }

        internal override bool TryGetBinding(
            BindingName name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            return (_declarativeRecord._hasBindings && _declarativeRecord.TryGetBinding(name, strict, out binding, out value))
                   || _objectRecord.TryGetBinding(name, strict, out binding, out value);
        }

        /// <summary>
        ///     http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2.2
        /// </summary>
        public override void CreateMutableBinding(string name, bool canBeDeleted = false)
        {
            if (_declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name))
            {
                ExceptionHelper.ThrowTypeError(_engine, name + " has already been declared");
            }

            _declarativeRecord.CreateMutableBinding(name, canBeDeleted);
        }

        public override void CreateImmutableBinding(string name, bool strict = true)
        {
            if (_declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name))
            {
                ExceptionHelper.ThrowTypeError(_engine, name + " has already been declared");
            }

            _declarativeRecord.CreateImmutableBinding(name, strict);
        }

        public override void InitializeBinding(string name, JsValue value)
        {
            if (_declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name))
            {
                _declarativeRecord.InitializeBinding(name, value);
            }
            else
            {
                _objectRecord.InitializeBinding(name, value);
            }
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            if (_declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name))
            {
                _declarativeRecord.SetMutableBinding(name, value, strict);
            }
            else
            {
                _objectRecord.SetMutableBinding(name, value, strict);
            }
        }

        internal override void SetMutableBinding(BindingName name, JsValue value, bool strict)
        {
            if (_declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name.Key.Name))
            {
                _declarativeRecord.SetMutableBinding(name.Key.Name, value, strict);
            }
            else
            {
                _objectRecord.SetMutableBinding(name, value, strict);
            }
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            return _declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name)
                ? _declarativeRecord.GetBindingValue(name, strict)
                : _objectRecord.GetBindingValue(name, strict);
        }

        public override bool DeleteBinding(string name)
        {
            if (_declarativeRecord._hasBindings && _declarativeRecord.HasBinding(name))
            {
                return _declarativeRecord.DeleteBinding(name);
            }

            if (_objectRecord._bindingObject.HasOwnProperty(name))
            {
                var status = _objectRecord.DeleteBinding(name);
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
            return _objectRecord._bindingObject;
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
            var globalObject = _objectRecord._bindingObject;
            var existingProp = globalObject.GetOwnProperty(name);
            if (existingProp == PropertyDescriptor.Undefined)
            {
                return false;
            }

            return !existingProp.Configurable;
        }

        public bool CanDeclareGlobalVar(string name)
        {
            var globalObject = _objectRecord._bindingObject;
            if (globalObject.HasOwnProperty(name))
            {
                return true;
            }

            return globalObject.Extensible;
        }

        public bool CanDeclareGlobalFunction(string name)
        {
            var globalObject = _objectRecord._bindingObject;
            var existingProp = globalObject.GetOwnProperty(name);
            if (existingProp == PropertyDescriptor.Undefined)
            {
                return globalObject.Extensible;
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
            var globalObject = _objectRecord._bindingObject;
            var hasProperty = globalObject.HasOwnProperty(name);
            var extensible = globalObject.Extensible;
            if (!hasProperty && extensible)
            {
                _objectRecord.CreateMutableBinding(name, canBeDeleted);
                _objectRecord.InitializeBinding(name, Undefined);
            }

            _varNames.Add(name);
        }

        public void CreateGlobalFunctionBinding(string name, JsValue value, bool canBeDeleted)
        {
            var globalObject = _objectRecord._bindingObject;
            var existingProp = globalObject.GetOwnProperty(name);

            PropertyDescriptor desc;
            if (existingProp == PropertyDescriptor.Undefined || existingProp.Configurable)
            {
                desc = new PropertyDescriptor(value, true, true, canBeDeleted);
            }
            else
            {
                desc = new PropertyDescriptor(value, PropertyFlag.None);
            }

            globalObject.DefinePropertyOrThrow(name, desc);
            globalObject.Set(name, value, false);
            _varNames.Add(name);
        }

        public override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        internal override string[] GetAllBindingNames()
        {
            return _objectRecord._bindingObject.GetOwnProperties().Select(x => x.Key.ToString()).ToArray();
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(_objectRecord, other);
        }
    }
}