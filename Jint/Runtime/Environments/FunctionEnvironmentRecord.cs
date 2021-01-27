using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-function-environment-records
    /// </summary>
    internal sealed class FunctionEnvironmentRecord : DeclarativeEnvironmentRecord
    {
        private enum ThisBindingStatus
        {
            Lexical,
            Initialized,
            Uninitialized
        }

        private JsValue _thisValue;
        private ThisBindingStatus _thisBindingStatus;
        internal readonly FunctionInstance _functionObject;

        public FunctionEnvironmentRecord(
            Engine engine, 
            FunctionInstance functionObject,
            JsValue newTarget) : base(engine)
        {
            _functionObject = functionObject;
            NewTarget = newTarget;
            if (functionObject._functionDefinition.Function is ArrowFunctionExpression)
            {
                _thisBindingStatus = ThisBindingStatus.Lexical;
            }
            else
            {
                _thisBindingStatus = ThisBindingStatus.Uninitialized;
            }
        }


        public override bool HasThisBinding() => _thisBindingStatus != ThisBindingStatus.Lexical;

        public override bool HasSuperBinding() => 
            _thisBindingStatus != ThisBindingStatus.Lexical && !_functionObject._homeObject.IsUndefined();

        public override JsValue WithBaseObject()
        {
            return _thisBindingStatus == ThisBindingStatus.Uninitialized
                ? ExceptionHelper.ThrowReferenceError<JsValue>(_engine)
                : _thisValue;
        }

        public JsValue BindThisValue(JsValue value)
        {
            if (_thisBindingStatus == ThisBindingStatus.Initialized)
            {
                ExceptionHelper.ThrowReferenceError<JsValue>(_engine);
            }
            _thisValue = value;
            _thisBindingStatus = ThisBindingStatus.Initialized;
            return value;
        }

        public override JsValue GetThisBinding()
        {
            return _thisBindingStatus == ThisBindingStatus.Uninitialized 
                ? ExceptionHelper.ThrowReferenceError<JsValue>(_engine)
                : _thisValue;
        }

        public JsValue GetSuperBase()
        {
            var home = _functionObject._homeObject;
            return home.IsUndefined() 
                ? Undefined
                : ((ObjectInstance) home).GetPrototypeOf();
        }

        // optimization to have logic near record internal structures.

        internal void InitializeParameters(
            Key[] parameterNames,
            bool hasDuplicates, 
            JsValue[] arguments)
        {
            var value = hasDuplicates ? Undefined : null;
            var directSet = !hasDuplicates && _dictionary.Count == 0;
            for (var i = 0; (uint) i < (uint) parameterNames.Length; i++)
            {
                var paramName = parameterNames[i];
                if (directSet || !_dictionary.ContainsKey(paramName))
                {
                    var parameterValue = value;
                    if (arguments != null)
                    {
                        parameterValue = (uint) i < (uint) arguments.Length ? arguments[i] : Undefined;
                    }

                    _dictionary[paramName] = new Binding(parameterValue, canBeDeleted: false, mutable: true, strict: false);
                }
            }
        }
        
        internal void AddFunctionParameters(IFunction functionDeclaration, JsValue[] arguments)
        {
            bool empty = _dictionary.Count == 0;
            ref readonly var parameters = ref functionDeclaration.Params;
            var count = parameters.Count;
            for (var i = 0; i < count; i++)
            {
                SetFunctionParameter(parameters[i], arguments, i, empty);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFunctionParameter(
            Node parameter,
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
            Node parameter,
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
                HandleArrayPattern(initiallyEmpty, argument, arrayPattern);
            }
            else if (parameter is ObjectPattern objectPattern)
            {
                HandleObjectPattern(initiallyEmpty, argument, objectPattern);
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

        private void HandleObjectPattern(bool initiallyEmpty, JsValue argument, ObjectPattern objectPattern)
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

            var processedProperties = objectPattern.Properties.Count > 0 &&
                                      objectPattern.Properties[objectPattern.Properties.Count - 1] is RestElement
                ? new HashSet<JsValue>()
                : null;

            var jsValues = _engine._jsValueArrayPool.RentArray(1);
            foreach (var property in objectPattern.Properties)
            {
                var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                var paramVarEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                _engine.EnterExecutionContext(paramVarEnv, paramVarEnv);

                try
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
                            var jintCallExpression = new JintCallExpression(_engine, callExpression);
                            var jsValue = jintCallExpression.GetValue();
                            propertyName = TypeConverter.ToJsString(jsValue);
                        }
                        else
                        {
                            propertyName = ExceptionHelper.ThrowArgumentOutOfRangeException<JsString>("property", "unknown object pattern property type");
                        }

                        processedProperties?.Add(propertyName.ToString());
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
                finally
                {
                    _engine.LeaveExecutionContext();
                }
            }

            _engine._jsValueArrayPool.ReturnArray(jsValues);
        }

        private void HandleArrayPattern(bool initiallyEmpty, JsValue argument, ArrayPattern arrayPattern)
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
            Node left,
            Node right,
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
                var expression = right.As<Expression>();
                var jintExpression = JintExpression.Build(_engine, expression);

                var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                var paramVarEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);

                _engine.EnterExecutionContext(paramVarEnv, paramVarEnv);
                try
                {
                    argument = jintExpression.GetValue();
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetItemSafely(Key name, JsValue argument, bool initiallyEmpty)
        {
            if (initiallyEmpty)
            {
                _dictionary[name] = new Binding(argument, canBeDeleted: false, mutable: true, strict: false);
            }
            else
            {
                SetItemCheckExisting(name, argument);
            }
        }
        
        private void SetItemCheckExisting(Key name, JsValue argument)
        {
            if (!_dictionary.TryGetValue(name, out var existing))
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
