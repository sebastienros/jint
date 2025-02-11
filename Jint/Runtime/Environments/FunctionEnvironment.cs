using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Environments;

/// <summary>
/// https://tc39.es/ecma262/#sec-function-environment-records
/// </summary>
internal sealed class FunctionEnvironment : DeclarativeEnvironment
{
    private enum ThisBindingStatus
    {
        Lexical,
        Initialized,
        Uninitialized
    }

    private JsValue? _thisValue;
    private ThisBindingStatus _thisBindingStatus;
    internal readonly Function _functionObject;

    public FunctionEnvironment(
        Engine engine,
        Function functionObject,
        JsValue newTarget) : base(engine)
    {
        _functionObject = functionObject;
        NewTarget = newTarget;
        if (functionObject._functionDefinition?.Function.Type is NodeType.ArrowFunctionExpression)
        {
            _thisBindingStatus = ThisBindingStatus.Lexical;
        }
        else
        {
            _thisBindingStatus = ThisBindingStatus.Uninitialized;
        }
    }


    internal override bool HasThisBinding() => _thisBindingStatus != ThisBindingStatus.Lexical;

    internal override bool HasSuperBinding() =>
        _thisBindingStatus != ThisBindingStatus.Lexical && !_functionObject._homeObject.IsUndefined();

    public JsValue BindThisValue(JsValue value)
    {
        if (_thisBindingStatus != ThisBindingStatus.Initialized)
        {
            _thisValue = value;
            _thisBindingStatus = ThisBindingStatus.Initialized;
            return value;
        }

        ExceptionHelper.ThrowReferenceError(_functionObject._realm, "'this' has already been bound");
        return null!;
    }

    internal override JsValue GetThisBinding()
    {
        if (_thisBindingStatus != ThisBindingStatus.Uninitialized)
        {
            return _thisValue!;
        }

        ThrowUninitializedThis();
        return null!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUninitializedThis()
    {
        var message = "Cannot access uninitialized 'this'";
        if (NewTarget is ScriptFunction { _isClassConstructor: true, _constructorKind: ConstructorKind.Derived })
        {
            // help with better error message
            message = "Must call super constructor in derived class before accessing 'this' or returning from derived constructor";
        }

        ExceptionHelper.ThrowReferenceError(_engine.ExecutionContext.Realm, message);
    }

    public JsValue GetSuperBase()
    {
        var home = _functionObject._homeObject;
        return home.IsUndefined()
            ? Undefined
            : ((ObjectInstance) home).GetPrototypeOf() ?? Null;
    }

    // optimization to have logic near record internal structures.

    internal void InitializeParameters(
        Key[] parameterNames,
        bool hasDuplicates,
        JsCallArguments? arguments)
    {
        if (parameterNames.Length == 0)
        {
            return;
        }

        var value = hasDuplicates ? Undefined : null;
        var directSet = !hasDuplicates && (_dictionary is null || _dictionary.Count == 0);
        _dictionary ??= new HybridDictionary<Binding>(parameterNames.Length, checkExistingKeys: !directSet);
        for (uint i = 0; i < (uint) parameterNames.Length; i++)
        {
            var paramName = parameterNames[i];
            ref var binding = ref _dictionary.GetValueRefOrAddDefault(paramName, out var exists);
            if (directSet || !exists)
            {
                var parameterValue = arguments?.At((int) i, Undefined) ?? value;
                binding = new Binding(parameterValue!, canBeDeleted: false, mutable: true, strict: false);
            }
        }

        _dictionary.CheckExistingKeys = true;
    }

    internal void AddFunctionParameters(EvaluationContext context, IFunction functionDeclaration, JsCallArguments arguments)
    {
        var empty = _dictionary is null || _dictionary.Count == 0;
        ref readonly var parameters = ref functionDeclaration.Params;
        var count = parameters.Count;
        for (var i = 0; i < count; i++)
        {
            SetFunctionParameter(context, parameters[i], arguments, i, empty);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFunctionParameter(
        EvaluationContext context,
        Node? parameter,
        JsCallArguments arguments,
        int index,
        bool initiallyEmpty)
    {
        if (parameter is Identifier identifier)
        {
            var argument = arguments.At(index);
            SetItemSafely(identifier.Name, argument, initiallyEmpty);
        }
        else
        {
            SetFunctionParameterUnlikely(context, parameter, arguments, index, initiallyEmpty);
        }
    }

    private void SetFunctionParameterUnlikely(
        EvaluationContext context,
        Node? parameter,
        JsCallArguments arguments,
        int index,
        bool initiallyEmpty)
    {
        var argument = arguments.At(index);

        if (parameter is RestElement restElement)
        {
            HandleRestElementArray(context, restElement, arguments, index, initiallyEmpty);
        }
        else if (parameter is ArrayPattern arrayPattern)
        {
            HandleArrayPattern(context, initiallyEmpty, argument, arrayPattern);
        }
        else if (parameter is ObjectPattern objectPattern)
        {
            HandleObjectPattern(context, initiallyEmpty, argument, objectPattern);
        }
        else if (parameter is AssignmentPattern assignmentPattern)
        {
            HandleAssignmentPatternOrExpression(context, assignmentPattern.Left, assignmentPattern.Right, argument, initiallyEmpty);
        }
        else if (parameter is AssignmentExpression assignmentExpression)
        {
            HandleAssignmentPatternOrExpression(context, assignmentExpression.Left, assignmentExpression.Right, argument, initiallyEmpty);
        }
    }

    private void HandleObjectPattern(EvaluationContext context, bool initiallyEmpty, JsValue argument, ObjectPattern objectPattern)
    {
        if (argument.IsNullOrUndefined())
        {
            ExceptionHelper.ThrowTypeError(_functionObject._realm, "Destructed parameter is null or undefined");
        }

        var argumentObject = TypeConverter.ToObject(_engine.Realm , argument);

        ref readonly var properties = ref objectPattern.Properties;
        var processedProperties = properties.Count > 0 && properties[properties.Count - 1] is RestElement
            ? new HashSet<JsValue>()
            : null;

        var jsValues = _engine._jsValueArrayPool.RentArray(1);
        foreach (var property in properties)
        {
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var paramVarEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
            PrivateEnvironment? privateEnvironment = null; // TODO PRIVATE check when implemented
            _engine.EnterExecutionContext(paramVarEnv, paramVarEnv, _engine.ExecutionContext.Realm, privateEnvironment);

            try
            {
                if (property is AssignmentProperty p)
                {
                    var propertyName = p.GetKey(_engine);
                    processedProperties?.Add(propertyName.ToString());
                    jsValues[0] = argumentObject.Get(propertyName);
                    SetFunctionParameter(context, p.Value, jsValues, 0, initiallyEmpty);
                }
                else
                {
                    if (((RestElement) property).Argument is Identifier restIdentifier)
                    {
                        var rest = _engine.Realm.Intrinsics.Object.Construct((argumentObject.Properties?.Count ?? 0) - processedProperties!.Count);
                        argumentObject.CopyDataProperties(rest, processedProperties);
                        SetItemSafely(restIdentifier.Name, rest, initiallyEmpty);
                    }
                    else
                    {
                        ExceptionHelper.ThrowSyntaxError(_functionObject._realm, "Object rest parameter can only be objects");
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

    private void HandleArrayPattern(EvaluationContext context, bool initiallyEmpty, JsValue argument, ArrayPattern arrayPattern)
    {
        if (argument.IsNull())
        {
            ExceptionHelper.ThrowTypeError(_functionObject._realm, "Destructed parameter is null");
        }

        JsArray? array;
        if (argument is JsArray { HasOriginalIterator: true } ai)
        {
            array = ai;
        }
        else
        {
            if (!argument.TryGetIterator(_functionObject._realm, out var iterator))
            {
                ExceptionHelper.ThrowTypeError(context.Engine.Realm, "object is not iterable");
            }

            array = _engine.Realm.Intrinsics.Array.ArrayCreate(0);
            var max = arrayPattern.Elements.Count;
            if (max > 0 && arrayPattern.Elements[max - 1]?.Type == NodeType.RestElement)
            {
                // need to consume all
                max = int.MaxValue;
            }
            var protocol = new ArrayPatternProtocol(_engine, array, iterator, max);
            protocol.Execute();
        }

        var arrayContents = array.ToArray();
        for (var i = 0; i < arrayPattern.Elements.Count; i++)
        {
            SetFunctionParameter(context, arrayPattern.Elements[i], arrayContents, i, initiallyEmpty);
        }
    }

    private void HandleRestElementArray(
        EvaluationContext context,
        RestElement restElement,
        JsCallArguments arguments,
        int index,
        bool initiallyEmpty)
    {
        // index + 1 == parameters.count because rest is last
        int restCount = arguments.Length - (index + 1) + 1;
        uint count = restCount > 0 ? (uint) restCount : 0;

        uint targetIndex = 0;
        var rest = new JsValue[count];
        for (var argIndex = index; argIndex < arguments.Length; ++argIndex)
        {
            rest[targetIndex++] = arguments[argIndex];
        }

        var array = new JsArray(_engine, rest);
        if (restElement.Argument is Identifier restIdentifier)
        {
            SetItemSafely(restIdentifier.Name, array, initiallyEmpty);
        }
        else if (restElement.Argument is DestructuringPattern pattern)
        {
            SetFunctionParameter(context, pattern, [array], 0, initiallyEmpty);
        }
        else
        {
            ExceptionHelper.ThrowSyntaxError(_functionObject._realm, "Rest parameters can only be identifiers or arrays");
        }
    }

    private void HandleAssignmentPatternOrExpression(
        EvaluationContext context,
        Node left,
        Node right,
        JsValue argument,
        bool initiallyEmpty)
    {
        var idLeft = left as Identifier;
        if (idLeft != null
            && right is Identifier idRight
            && string.Equals(idLeft.Name, idRight.Name, StringComparison.Ordinal))
        {
            ExceptionHelper.ThrowReferenceNameError(_functionObject._realm, idRight.Name);
        }

        if (argument.IsUndefined())
        {
            var expression = (Expression) right;
            var jintExpression = JintExpression.Build(expression);

            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var paramVarEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);

            _engine.EnterExecutionContext(new ExecutionContext(null, paramVarEnv, paramVarEnv, null, _engine.Realm, null));
            try
            {
                argument = jintExpression.GetValue(context);
            }
            finally
            {
                _engine.LeaveExecutionContext();
            }

            if (idLeft != null && right.IsFunctionDefinition())
            {
                ((Function) argument).SetFunctionName(idLeft.Name);
            }
        }

        SetFunctionParameter(context, left, [argument], 0, initiallyEmpty);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetItemSafely(Key name, JsValue argument, bool initiallyEmpty)
    {
        if (initiallyEmpty)
        {
            _dictionary ??= new HybridDictionary<Binding>();
            _dictionary[name] = new Binding(argument, canBeDeleted: false, mutable: true, strict: false);
        }
        else
        {
            SetItemCheckExisting(name, argument);
        }
    }

    private void SetItemCheckExisting(Key name, JsValue argument)
    {
        _dictionary ??= new HybridDictionary<Binding>();
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
                ExceptionHelper.ThrowTypeError(_functionObject._realm, "Can't update the value of an immutable binding.");
            }
        }
    }

    private sealed class ArrayPatternProtocol : IteratorProtocol
    {
        private readonly JsArray _instance;
        private readonly int _max;
        private long _index;

        public ArrayPatternProtocol(
            Engine engine,
            JsArray instance,
            IteratorInstance iterator,
            int max) : base(engine, iterator, 0)
        {
            _instance = instance;
            _max = max;
        }

        protected override void ProcessItem(JsValue[] arguments, JsValue currentValue)
        {
            _instance.SetIndexValue((uint) _index, currentValue, updateLength: false);
            _index++;
        }

        protected override bool ShouldContinue => _index < _max;

        protected override void IterationEnd()
        {
            if (_index > 0)
            {
                _instance.SetLength((uint) _index);
                IteratorClose(CompletionType.Normal);
            }
        }
    }
}
