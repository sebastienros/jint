using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a declarative environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.1
    /// </summary>
    internal sealed class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        private readonly HybridDictionary<Binding> _dictionary = new HybridDictionary<Binding>();

        public DeclarativeEnvironmentRecord(Engine engine) : base(engine)
        {
        }

        private bool ContainsKey(in Key key)
        {
            return _dictionary.ContainsKey(key);
        }

        private bool TryGetValue(in Key key, out Binding value)
        {
            value = default;
            return _dictionary.TryGetValue(key, out value);
        }

        public override bool HasBinding(string name)
        {
            return ContainsKey(name);
        }

        internal override bool TryGetBinding(
            in Key name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            binding = default;
            var success = _dictionary.TryGetValue(name, out binding);
            value = success ? UnwrapBindingValue(strict, binding) : default;
            return success;
        }

        public override void CreateMutableBinding(string name, bool canBeDeleted = false)
        {
            _dictionary[name] = new Binding(null, canBeDeleted, mutable: true, strict: false);
        }
        
        public override void CreateImmutableBinding(string name, bool strict = true)
        {
            _dictionary[name] = new Binding(null, canBeDeleted: false, mutable: false, strict: false);
        }

        public override void InitializeBinding(string name, JsValue value)
        {
            _dictionary.TryGetValue(name, out var binding);
            _dictionary[name] = binding.ChangeValue(value);
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            if (!_dictionary.TryGetValue(name, out var binding))
            {
                if (strict)
                {
                    ExceptionHelper.ThrowReferenceError(_engine, name);
                }
                CreateMutableBinding(name, true);
                InitializeBinding(name, value);
                return;
            }

            if (binding.Strict)
            {
                strict = true;
            }
            
            // Is it an uninitialized binding?
            if (!binding.IsInitialized())
            {
                ExceptionHelper.ThrowReferenceError(_engine, name);
            }
            
            if (binding.Mutable)
            {
                _dictionary[name] = binding.ChangeValue(value);
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

        public override bool DeleteBinding(string name)
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

            return true;
        }

        public override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        /// <inheritdoc />
        internal override string[] GetAllBindingNames()
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

        internal void AddFunctionParameters(
            JsValue[] arguments,
            ArgumentsInstance argumentsInstance,
            IFunction functionDeclaration)
        {
            bool empty = _dictionary.Count == 0;
            if (!(argumentsInstance is null))
            {
                _dictionary[KnownKeys.Arguments] = new Binding(argumentsInstance, canBeDeleted: false, mutable: true, strict: false);
            }

            ref readonly var parameters = ref functionDeclaration.Params;
            var count = parameters.Count;
            for (var i = 0; i < count; i++)
            {
                SetFunctionParameter(parameters[i], arguments, i, empty);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFunctionParameter(
            INode parameter,
            JsValue[] arguments,
            int index,
            bool initiallyEmpty)
        {
            if (parameter is Identifier identifier)
            {
                var argument = (uint) index < (uint) arguments.Length ? arguments[index] : Undefined;
                SetItemSafely(identifier.Name, argument, initiallyEmpty);
            }
            else
            {
                SetFunctionParameterUnlikely(parameter, arguments, index, initiallyEmpty);
            }
        }

        private void SetFunctionParameterUnlikely(
            INode parameter,
            JsValue[] arguments,
            int index,
            bool initiallyEmpty)
        {
            var argument = arguments.Length > index ? arguments[index] : Undefined;

            if (parameter is RestElement restElement)
            {
                HandleRestElementArray(restElement, arguments, index, initiallyEmpty);
            }
            else if (parameter is ArrayPattern arrayPattern)
            {
                if (argument.IsNull())
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Destructed parameter is null");
                }

                ArrayInstance array = null;
                var arrayContents = System.Array.Empty<JsValue>();
                if (argument.IsArray())
                {
                    array = argument.AsArray();
                }
                else if (argument.IsObject() && argument.TryGetIterator(_engine, out var iterator))
                {
                    array = _engine.Array.ConstructFast(0);
                    var protocol = new ArrayPatternProtocol(_engine, array, iterator, arrayPattern.Elements.Count);
                    protocol.Execute();
                }

                if (!ReferenceEquals(array, null))
                {
                    arrayContents = new JsValue[array.GetLength()];

                    for (uint i = 0; i < (uint) arrayContents.Length; i++)
                    {
                        arrayContents[i] = array.Get(i);
                    }
                }

                for (uint arrayIndex = 0; arrayIndex < arrayPattern.Elements.Count; arrayIndex++)
                {
                    SetFunctionParameter(arrayPattern.Elements[(int) arrayIndex], arrayContents, (int) arrayIndex, initiallyEmpty);
                }
            }
            else if (parameter is ObjectPattern objectPattern)
            {
                if (argument.IsNullOrUndefined())
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Destructed parameter is null or undefined");
                }

                if (!argument.IsObject())
                {
                    return;
                }

                var argumentObject = argument.AsObject();

                var processedProperties = objectPattern.Properties.Count > 0 && objectPattern.Properties[objectPattern.Properties.Count - 1] is RestElement
                    ? new HashSet<JsValue>()
                    : null;

                var jsValues = _engine._jsValueArrayPool.RentArray(1);
                foreach (var property in objectPattern.Properties)
                {
                    if (property is Property p)
                    {
                        JsString propertyName;
                        if (p.Key is Identifier propertyIdentifier)
                        {
                            propertyName = JsString.Create(propertyIdentifier.Name);
                        }
                        else if (p.Key is Literal propertyLiteral)
                        {
                            propertyName = JsString.Create(propertyLiteral.Raw);
                        }
                        else if (p.Key is CallExpression callExpression)
                        {
                            var jintCallExpression = JintExpression.Build(_engine, callExpression);
                            propertyName = (JsString) jintCallExpression.GetValue();
                        }
                        else
                        {
                            propertyName = ExceptionHelper.ThrowArgumentOutOfRangeException<JsString>("property", "unknown object pattern property type");
                        }

                        processedProperties?.Add(propertyName.AsStringWithoutTypeCheck());
                        jsValues[0] = argumentObject.Get(propertyName);
                        SetFunctionParameter(p.Value, jsValues, 0, initiallyEmpty);
                    }
                    else
                    {
                        if (((RestElement) property).Argument is Identifier restIdentifier)
                        {
                            var rest = _engine.Object.Construct(argumentObject.Properties.Count - processedProperties.Count);
                            argumentObject.CopyDataProperties(rest, processedProperties);
                            SetItemSafely(restIdentifier.Name, rest, initiallyEmpty);
                        }
                        else
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine, "Object rest parameter can only be objects");
                        }
                    }
                }
                _engine._jsValueArrayPool.ReturnArray(jsValues);
            }
            else if (parameter is AssignmentPattern assignmentPattern)
            {
                HandleAssignmentPatternOrExpression(assignmentPattern.Left, assignmentPattern.Right, argument, initiallyEmpty);
            }
            else if (parameter is AssignmentExpression assignmentExpression)
            {
                HandleAssignmentPatternOrExpression(assignmentExpression.Left, assignmentExpression.Right, argument, initiallyEmpty);
            }
        }

        private void HandleRestElementArray(
            RestElement restElement,
            JsValue[] arguments,
            int index,
            bool initiallyEmpty)
        {
            // index + 1 == parameters.count because rest is last
            int restCount = arguments.Length - (index + 1) + 1;
            uint count = restCount > 0 ? (uint) restCount : 0;

            var rest = _engine.Array.ConstructFast(count);

            uint targetIndex = 0;
            for (var argIndex = index; argIndex < arguments.Length; ++argIndex)
            {
                rest.SetIndexValue(targetIndex++, arguments[argIndex], updateLength: false);
            }

            if (restElement.Argument is Identifier restIdentifier)
            {
                SetItemSafely(restIdentifier.Name, rest, initiallyEmpty);
            }
            else if (restElement.Argument is BindingPattern bindingPattern)
            {
                SetFunctionParameter(bindingPattern, new JsValue[]
                {
                    rest
                }, index, initiallyEmpty);
            }
            else
            {
                ExceptionHelper.ThrowSyntaxError(_engine, "Rest parameters can only be identifiers or arrays");
            }
        }

        private void HandleAssignmentPatternOrExpression(
            INode left,
            INode right,
            JsValue argument,
            bool initiallyEmpty)
        {
            var idLeft = left as Identifier;
            if (idLeft != null
                && right is Identifier idRight
                && idLeft.Name == idRight.Name)
            {
                ExceptionHelper.ThrowReferenceError(_engine, idRight.Name);
            }

            if (argument.IsUndefined())
            {
                JsValue RunInNewParameterEnvironment(JintExpression exp)
                {
                    var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var paramVarEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);

                    _engine.EnterExecutionContext(paramVarEnv, paramVarEnv, _engine.ExecutionContext.ThisBinding);
                    var result = exp.GetValue();
                    _engine.LeaveExecutionContext();

                    return result;
                }

                var expression = right.As<Expression>();
                var jintExpression = JintExpression.Build(_engine, expression);

                argument = jintExpression is JintSequenceExpression
                    ? RunInNewParameterEnvironment(jintExpression)
                    : jintExpression.GetValue();

                if (idLeft != null && right.IsFunctionWithName())
                {
                    ((FunctionInstance) argument).SetFunctionName(idLeft.Name);
                }
            }

            SetFunctionParameter(left, new[]
            {
                argument
            }, 0, initiallyEmpty);
        }

        private void SetItemSafely(in Key name, JsValue argument, bool initiallyEmpty)
        {
            if (initiallyEmpty || !TryGetValue(name, out var existing))
            {
                _dictionary[name] = new Binding(argument, canBeDeleted: false, mutable: true, strict: false);
            }
            else
            {
                if (existing.Mutable)
                {
                    _dictionary[name] = existing.ChangeValue(argument);
                }
                else
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Can't update the value of an immutable binding.");
                }
            }
        }

        internal static JsValue HandleAssignmentPatternIfNeeded(IFunction functionDeclaration, JsValue jsValue, uint index)
        {
            // TODO remove this method, overwrite with above SetFunctionParameter logic
            if (jsValue.IsUndefined()
                && index < functionDeclaration?.Params.Count
                && functionDeclaration.Params[(int) index] is AssignmentPattern ap
                && ap.Right is Literal l)
            {
                return JintLiteralExpression.ConvertToJsValue(l);
            }

            return jsValue;
        }

        private sealed class ArrayPatternProtocol : IteratorProtocol
        {
            private readonly ArrayInstance _instance;
            private readonly int _max;
            private long _index = -1;

            public ArrayPatternProtocol(
                Engine engine,
                ArrayInstance instance,
                IIterator iterator,
                int max) : base(engine, iterator, 0)
            {
                _instance = instance;
                _max = max;
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                _index++;
                var jsValue = ExtractValueFromIteratorInstance(currentValue);
                _instance.SetIndexValue((uint) _index, jsValue, updateLength: false);
            }

            protected override bool ShouldContinue => _index < _max;

            protected override void IterationEnd()
            {
                if (_index >= 0)
                {
                    _instance.SetLength((uint) _index);
                    IteratorClose(CompletionType.Normal);
                }
            }
        }
    }
}
