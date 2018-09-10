﻿using System;
using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Native.Function;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a declarative environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.1
    /// </summary>
    public sealed class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        private StructDictionary<Binding> _dictionary;
        private bool _set;
        private string _key;
        private Binding _value;

        private const string BindingNameArguments = "arguments";
        private Binding _argumentsBinding;

        public DeclarativeEnvironmentRecord(Engine engine) : base(engine)
        {
        }

        private void SetItem(string key, in Binding value)
        {
            if (_set && _key != key)
            {
                if (_dictionary == null)
                {
                    _dictionary = new StructDictionary<Binding>();
                }

                _dictionary.TryInsert(_key, _value, InsertionBehavior.OverwriteExisting);
            }

            _set = true;
            _key = key;
            _value = value;

            _dictionary?.TryInsert(key, value, InsertionBehavior.OverwriteExisting);
        }

        private ref Binding GetExistingItem(string key)
        {
            if (_set && _key == key)
            {
                return ref _value;
            }

            if (key.Length == 9 && key == BindingNameArguments)
            {
                return ref _argumentsBinding;
            }

            return ref _dictionary.GetItem(key);
        }

        private bool ContainsKey(string key)
        {
            if (key.Length == 9 && key == BindingNameArguments)
            {
                return !ReferenceEquals(_argumentsBinding.Value, null);
            }

            if (_set && key == _key)
            {
                return true;
            }

            return _dictionary?.ContainsKey(key) == true;
        }

        private void Remove(string key)
        {
            if (_set && key == _key)
            {
                _set = false;
                _key = null;
                _value = default;
            }
            
            if (key == BindingNameArguments)
            {
                _argumentsBinding.Value = null;
            }
            else
            {
                _dictionary?.Remove(key);
            }

        }

        private bool TryGetValue(string key, out Binding value)
        {
            value = default;
            if (_set && _key == key)
            {
                value = _value;
                return true;
            }

            return _dictionary?.TryGetValue(key, out value) == true;
        }

        public override bool HasBinding(string name)
        {
            return ContainsKey(name);
        }

        public override void CreateMutableBinding(string name, JsValue value, bool canBeDeleted = false)
        {
            SetItem(name, new Binding(value, canBeDeleted, mutable: true));
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            ref var binding = ref GetExistingItem(name);

            if (binding.Mutable)
            {
                binding.Value = value;
            }
            else
            {
                if (strict)
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Can't update the value of an immutable binding.");
                }
            }
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            ref var binding = ref GetExistingItem(name);

            if (!binding.Mutable && binding.Value._type == Types.Undefined)
            {
                if (strict)
                {
                    ExceptionHelper.ThrowReferenceError(_engine, "Can't access an uninitialized immutable binding.");
                }

                return Undefined;
            }

            return binding.Value;
        }

        public override bool DeleteBinding(string name)
        {
            ref Binding binding = ref GetExistingItem(name);

            if (ReferenceEquals(binding.Value, null))
            {
                return true;
            }

            if (!binding.CanBeDeleted)
            {
                return false;
            }

            Remove(name);

            return true;
        }

        public override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        /// <inheritdoc />
        public override string[] GetAllBindingNames()
        {
            int size = _set ? 1 : 0;
            if (!ReferenceEquals(_argumentsBinding.Value, null))
            {
                size += 1;
            }

            if (_dictionary != null)
            {
                size += _dictionary.Count;
            }

            var keys = size > 0 ? new string[size] : ArrayExt.Empty<string>();
            int n = 0;
            if (_set)
            {
                keys[n++] = _key;
            }

            if (!ReferenceEquals(_argumentsBinding.Value, null))
            {
                keys[n++] = BindingNameArguments;
            }

            if (_dictionary != null)
            {
                foreach (var key in _dictionary.Keys)
                {
                    keys[n++] = key;
                }
            }

            return keys;
        }

        internal void ReleaseArguments()
        {
            _engine._argumentsInstancePool.Return(_argumentsBinding.Value as ArgumentsInstance);
            _argumentsBinding = default;
        }

        /// <summary>
        /// Optimized version for function calls.
        /// </summary>
        internal void AddFunctionParameters(
            FunctionInstance functionInstance,
            JsValue[] arguments,
            ArgumentsInstance argumentsInstance)
        {
            var parameters = functionInstance._formalParameters;
            bool empty = _dictionary == null && !_set;
            if (empty && parameters.Length == 1 && parameters[0].Length != BindingNameArguments.Length)
            {
                var jsValue = arguments.Length == 0 ? Undefined : arguments[0];
                var binding = new Binding(jsValue, false, true);
                _set = true;
                _key = parameters[0];
                _value = binding;
            }
            else
            {
                AddMultipleParameters(arguments, parameters);
            }

            if (ReferenceEquals(_argumentsBinding.Value, null))
            {
                _argumentsBinding = new Binding(argumentsInstance, canBeDeleted: false, mutable: true);
            }
        }

        private void AddMultipleParameters(JsValue[] arguments, string[] parameters)
        {
            bool empty = _dictionary == null && !_set;
            for (var i = 0; i < parameters.Length; i++)
            {
                var argName = parameters[i];
                var jsValue = i + 1 > arguments.Length ? Undefined : arguments[i];

                if (empty || !TryGetValue(argName, out var existing))
                {
                    var binding = new Binding(jsValue, false, true);
                    if (argName.Length == 9 && argName == BindingNameArguments)
                    {
                        _argumentsBinding = binding;
                    }
                    else
                    {
                        SetItem(argName, binding);
                    }
                }
                else
                {
                    if (existing.Mutable)
                    {
                        ref var b = ref GetExistingItem(argName);
                        b.Value = jsValue;
                    }
                    else
                    {
                        ExceptionHelper.ThrowTypeError(_engine, "Can't update the value of an immutable binding.");
                    }
                }
            }
        }

        internal void AddVariableDeclarations(List<VariableDeclaration> variableDeclarations)
        {
            var variableDeclarationsCount = variableDeclarations.Count;
            for (var i = 0; i < variableDeclarationsCount; i++)
            {
                var variableDeclaration = variableDeclarations[i];
                var declarationsCount = variableDeclaration.Declarations.Count;
                for (var j = 0; j < declarationsCount; j++)
                {
                    var d = variableDeclaration.Declarations[j];
                    var dn = ((Identifier) d.Id).Name;

                    if (!ContainsKey(dn))
                    {
                        var binding = new Binding(Undefined, canBeDeleted: false, mutable: true);
                        SetItem(dn, binding);
                    }
                }
            }
        }
    }
}