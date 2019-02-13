﻿using System;
using System.Collections.Generic;
 using System.Runtime.CompilerServices;
 using Esprima.Ast;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Native.Function;
 using Jint.Runtime.Interpreter.Expressions;

 namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a declarative environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.1
    /// </summary>
    public sealed class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        private StringDictionarySlim<Binding> _dictionary;
        private bool _set;
        private string _key;
        private Binding _value;

        private const string BindingNameArguments = "arguments";
        private Binding _argumentsBinding;

        // false = not accessed, true = accessed, null = values copied
        private bool? _argumentsBindingWasAccessed = false;

        public DeclarativeEnvironmentRecord(Engine engine) : base(engine)
        {
        }

        private void SetItem(string key, in Binding value)
        {
            if (_set && _key != key)
            {
                if (_dictionary == null)
                {
                    _dictionary = new StringDictionarySlim<Binding>();
                }

                _dictionary[_key] = _value;
            }

            _set = true;
            _key = key;
            _value = value;

            if (_dictionary != null)
            {
                _dictionary[key] = value;
            }
        }

        private ref Binding GetExistingItem(string key)
        {
            if (_set && _key == key)
            {
                return ref _value;
            }

            if (key.Length == 9 && key == BindingNameArguments)
            {
                _argumentsBindingWasAccessed = true;
                return ref _argumentsBinding;
            }

            return ref _dictionary[key];
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

            return _dictionary != null && _dictionary.TryGetValue(key, out value);
        }

        public override bool HasBinding(string name)
        {
            return ContainsKey(name);
        }

        internal override bool TryGetBinding(string name, bool strict, out Binding binding)
        {
            if (_set && _key == name)
            {
                binding = _value;
                return true;
            }

            if (name.Length == 9
                && name == BindingNameArguments
                && !ReferenceEquals(_argumentsBinding.Value, null))
            {
                _argumentsBindingWasAccessed = true;
                binding = _argumentsBinding;
                return true;
            }

            if (_dictionary != null)
            {
                return _dictionary.TryGetValue(name, out binding);
            }

            binding = default;
            return false;
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
            return UnwrapBindingValue(name, strict, binding);
        }

        internal override JsValue UnwrapBindingValue(string name, bool strict, in Binding binding)
        {
            return UnwrapBindingValueInternal(strict, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue UnwrapBindingValueInternal(bool strict, in Binding binding)
        {
            if (!binding.Mutable && binding.Value._type == Types.Undefined)
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

            _dictionary?.Keys.CopyTo(keys, n);

            return keys;
        }

        /// <summary>
        /// Optimized version for function calls.
        /// </summary>
        internal void AddFunctionParameters(
            FunctionInstance functionInstance,
            JsValue[] arguments,
            ArgumentsInstance argumentsInstance,
            IFunction functionDeclaration)
        {
            var parameters = functionInstance._formalParameters;
            bool empty = _dictionary == null && !_set;
            if (empty && parameters.Length == 1 && parameters[0].Length != BindingNameArguments.Length)
            {
                var jsValue = arguments.Length == 0 ? Undefined : arguments[0];
                jsValue = HandleAssignmentPatternIfNeeded(functionDeclaration, jsValue, 0);
                jsValue = HandleRestPatternIfNeeded(_engine, functionDeclaration, arguments, 0, jsValue);
                HandleObjectPatternIfNeeded(_engine, functionDeclaration, jsValue, 0);

                var binding = new Binding(jsValue, false, true);
                _set = true;
                _key = parameters[0];
                _value = binding;
            }
            else
            {
                AddMultipleParameters(arguments, parameters, functionDeclaration);
            }

            if (ReferenceEquals(_argumentsBinding.Value, null))
            {
                _argumentsBinding = new Binding(argumentsInstance, canBeDeleted: false, mutable: true);
            }
        }

        private void AddMultipleParameters(JsValue[] arguments, string[] parameters, IFunction functionDeclaration)
        {
            bool empty = _dictionary == null && !_set;
            for (var i = 0; i < parameters.Length; i++)
            {
                var argName = parameters[i];
                var jsValue = i + 1 > arguments.Length ? Undefined : arguments[i];

                jsValue = HandleAssignmentPatternIfNeeded(functionDeclaration, jsValue, i);
                if (i == parameters.Length - 1)
                {
                    jsValue = HandleRestPatternIfNeeded(_engine, functionDeclaration, arguments, i, jsValue);
                }
                jsValue = HandleObjectPatternIfNeeded(_engine, functionDeclaration, jsValue, 0);

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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static JsValue HandleObjectPatternIfNeeded(Engine engine, IFunction functionDeclaration, JsValue jsValue, int index)
        {
            if (functionDeclaration.Params[index] is ObjectPattern op)
            {
                if (jsValue.IsNullOrUndefined())
                {
                    ExceptionHelper.ThrowTypeError(engine, "Cannot destructure 'undefined' or 'null'.");
                }
            }

            return jsValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static JsValue HandleAssignmentPatternIfNeeded(IFunction functionDeclaration, JsValue jsValue, int index)
        {
            if (jsValue.IsUndefined()
                && index < functionDeclaration?.Params.Count
                && functionDeclaration.Params[index] is AssignmentPattern ap
                && ap.Right is Literal l)
            {
                return JintLiteralExpression.ConvertToJsValue(l);
            }

            return jsValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static JsValue HandleRestPatternIfNeeded(
            Engine engine,
            IFunction functionDeclaration,
            JsValue[] arguments,
            int index,
            JsValue defaultValue)
        {
            if (index < functionDeclaration?.Params.Count
                && functionDeclaration.Params[index] is RestElement)
            {
                var count = (uint) (arguments.Length - functionDeclaration.Params.Count + 1);
                var rest = engine.Array.ConstructFast(count);

                uint targetIndex = 0;
                for (var i = index; i < arguments.Length; ++i)
                {
                    rest.SetIndexValue(targetIndex++, arguments[i], updateLength: false);
                }
                return rest;
            }

            return defaultValue;
        }

        internal void AddVariableDeclarations(Esprima.Ast.List<VariableDeclaration> variableDeclarations)
        {
            var variableDeclarationsCount = variableDeclarations.Count;
            for (var i = 0; i < variableDeclarationsCount; i++)
            {
                var variableDeclaration = variableDeclarations[i];
                var declarationsCount = variableDeclaration.Declarations.Count;
                for (var j = 0; j < declarationsCount; j++)
                {
                    var d = variableDeclaration.Declarations[j];
                    if (d.Id is Identifier id)
                    {
                        var dn = id.Name;
                        if (!ContainsKey(dn))
                        {
                            var binding = new Binding(Undefined, canBeDeleted: false, mutable: true);
                            SetItem(dn, binding);
                        }
                    }
                }
            }
        }
        
        internal override void FunctionWasCalled()
        {
            // we can safely release arguments only if it doesn't have possibility to escape the scope
            // so check if someone ever accessed it
            if (!(_argumentsBinding.Value is ArgumentsInstance argumentsInstance))
            {
                return;
            }
            
            if (!argumentsInstance._initialized && _argumentsBindingWasAccessed == false)
            {
                _engine._argumentsInstancePool.Return(argumentsInstance);
                _argumentsBinding = default;
            }
            else if (_argumentsBindingWasAccessed != null && argumentsInstance._args.Length > 0)
            {
                // we need to ensure we hold on to arguments given
                argumentsInstance.PersistArguments();
                _argumentsBindingWasAccessed = null;
            }
        }
    }
}