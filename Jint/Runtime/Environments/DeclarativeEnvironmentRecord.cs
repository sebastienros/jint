using System;
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
        private const string BindingNameArguments = "arguments";
        private Binding _argumentsBinding;

        private MruPropertyCache2<Binding> _bindings;

        public DeclarativeEnvironmentRecord(Engine engine) : base(engine)
        {
        }

        public override bool HasBinding(string name)
        {
            if (name.Length == 9 && name == BindingNameArguments)
            {
                return _argumentsBinding != null;
            }

            return _bindings?.ContainsKey(name) == true;
        }

        public override void CreateMutableBinding(string name, JsValue value, bool canBeDeleted = false)
        {
            var binding = new Binding
            {
                Value = value,
                CanBeDeleted = canBeDeleted,
                Mutable = true
            };

            if (name.Length == 9 && name == BindingNameArguments)
            {
                _argumentsBinding = binding;
            }
            else
            {
                if (_bindings == null)
                {
                    _bindings = new MruPropertyCache2<Binding>();
                }

                _bindings.Add(name, binding);
            }
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            if (_bindings == null)
            {
                _bindings = new MruPropertyCache2<Binding>();
            }

            var binding = name.Length == 9 && name == BindingNameArguments
                ? _argumentsBinding 
                : _bindings[name];

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
            var binding = name.Length == 9 && name == BindingNameArguments
                ? _argumentsBinding 
                : _bindings[name];

            if (!binding.Mutable && binding.Value.IsUndefined())
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
            Binding binding = null;
            if (name == BindingNameArguments)
            {
                binding = _argumentsBinding;
            }
            else
            {
                _bindings?.TryGetValue(name, out binding);
            }

            if (binding == null)
            {
                return true;
            }

            if (!binding.CanBeDeleted)
            {
                return false;
            }

            if (name == BindingNameArguments)
            {
                _argumentsBinding = null;
            }
            else
            {
                _bindings.Remove(name);
            }

            return true;
        }

        public override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        /// <summary>
        /// Creates a new immutable binding in an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding.</param>
        /// <param name="value">The value  of the binding.</param>
        public void CreateImmutableBinding(string name, JsValue value)
        {
            var binding = new Binding
            {
                Value = value,
                Mutable = false,
                CanBeDeleted = false
            };

            if (name == BindingNameArguments)
            {
                _argumentsBinding = binding;
            }
            else
            {
                if (_bindings == null)
                {
                    _bindings = new MruPropertyCache2<Binding>();
                }

                _bindings.Add(name, binding);
            }
        }

        /// <summary>
        /// Returns an array of all the defined binding names
        /// </summary>
        /// <returns>The array of all defined bindings</returns>
        public override string[] GetAllBindingNames()
        {
            return _bindings?.GetKeys() ?? Array.Empty<string>();
        }

        internal void ReleaseArguments()
        {
            _engine.ArgumentsInstancePool.Return(_argumentsBinding?.Value as ArgumentsInstance);
            _argumentsBinding = null;
        }

        /// <summary>
        /// Optimized version for function calls.
        /// </summary>
        internal void AddFunctionParameters(FunctionInstance functionInstance, JsValue[] arguments, ArgumentsInstance strict)
        {
            var parameters = functionInstance.FormalParameters;
            bool empty = _bindings == null;
            if (empty)
            {
                _bindings = new MruPropertyCache2<Binding>();
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                var argName = parameters[i];
                var jsValue = i + 1 > arguments.Length ? Undefined : arguments[i];

                if (empty || !_bindings.TryGetValue(argName, out var existing))
                {
                    var binding = new Binding
                    {
                        Value = jsValue,
                        CanBeDeleted = false,
                        Mutable = true
                    };

                    if (argName.Length == 9 && argName == BindingNameArguments)
                    {
                        _argumentsBinding = binding;
                    }
                    else
                    {
                        _bindings[argName] = binding;
                    }
                }
                else
                {
                    if (existing.Mutable)
                    {
                        existing.Value = jsValue;
                    }
                    else
                    {
                        ExceptionHelper.ThrowTypeError(_engine, "Can't update the value of an immutable binding.");
                    }
                }
            }

            if (_argumentsBinding == null)
            {
                _argumentsBinding = new Binding {Value = strict, Mutable = true};
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
                    if (_bindings == null)
                    {
                        _bindings = new MruPropertyCache2<Binding>();
                    }
                    
                    var d = variableDeclaration.Declarations[j];
                    var dn = ((Identifier) d.Id).Name;

                    if (!_bindings.ContainsKey(dn))
                    {
                        var binding = new Binding
                        {
                            Value = Undefined,
                            CanBeDeleted = false,
                            Mutable = true
                        };
                        _bindings.Add(dn, binding);
                    }
                }
            }
        }
    }
}
