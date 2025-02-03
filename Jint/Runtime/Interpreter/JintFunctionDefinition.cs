using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Generator;
using Jint.Native.Promise;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter;

/// <summary>
/// Works as memento for function execution. Optimization to cache things that don't change.
/// </summary>
internal sealed class JintFunctionDefinition
{
    private JintExpression? _bodyExpression;
    private JintStatementList? _bodyStatementList;

    public readonly string? Name;
    public readonly IFunction Function;

    public JintFunctionDefinition(IFunction function)
    {
        Function = function;
        Name = !string.IsNullOrEmpty(function.Id?.Name) ? function.Id!.Name : null;
    }

    public bool Strict => Function.IsStrict();

    public FunctionThisMode ThisMode => Function.IsStrict() ? FunctionThisMode.Strict : FunctionThisMode.Global;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarycallevaluatebody
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    internal Completion EvaluateBody(EvaluationContext context, Function functionObject, JsValue[] argumentsList) => EvaluateBodyAsync(context, functionObject, argumentsList).Preserve().GetAwaiter().GetResult();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarycallevaluatebody
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    internal async ValueTask<Completion> EvaluateBodyAsync(EvaluationContext context, Function functionObject, JsValue[] argumentsList)
    {
        Completion result;
        JsArguments? argumentsInstance = null;
        if (Function.Expression)
        {
            // https://tc39.es/ecma262/#sec-runtime-semantics-evaluateconcisebody
            _bodyExpression ??= JintExpression.Build((Expression) Function.Body);
            if (Function.Async)
            {
                // local copies to prevent capturing closure created on top of method
                var function = functionObject;
                var jsValues = argumentsList;

                var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
                await AsyncFunctionStart(context, promiseCapability, context =>
                {
                    context.Engine.FunctionDeclarationInstantiation(function, jsValues);
                    context.RunBeforeExecuteStatementChecks(Function.Body);
                    var jsValue = _bodyExpression.GetValue(context).Clone();
                    return new ValueTask<Completion>(new Completion(CompletionType.Return, jsValue, _bodyExpression._expression));
                }).ConfigureAwait(false);
                result = new Completion(CompletionType.Return, promiseCapability.PromiseInstance, Function.Body);
            }
            else
            {
                argumentsInstance = context.Engine.FunctionDeclarationInstantiation(functionObject, argumentsList);
                context.RunBeforeExecuteStatementChecks(Function.Body);
                var jsValue = _bodyExpression.GetValue(context).Clone();
                result = new Completion(CompletionType.Return, jsValue, Function.Body);
            }
        }
        else if (Function.Generator)
        {
            result = EvaluateGeneratorBody(context, functionObject, argumentsList);
        }
        else
        {
            if (Function.Async)
            {
                // local copies to prevent capturing closure created on top of method
                var function = functionObject;
                var arguments = argumentsList;

                var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
                _bodyStatementList ??= new JintStatementList(Function);
                await AsyncFunctionStart(context, promiseCapability, async context =>
                {
                    context.Engine.FunctionDeclarationInstantiation(function, arguments);
                    return await _bodyStatementList.ExecuteAsync(context).ConfigureAwait(false);
                }).ConfigureAwait(false);
                result = new Completion(CompletionType.Return, promiseCapability.PromiseInstance, Function.Body);
            }
            else
            {
                // https://tc39.es/ecma262/#sec-runtime-semantics-evaluatefunctionbody
                argumentsInstance = context.Engine.FunctionDeclarationInstantiation(functionObject, argumentsList);
                _bodyStatementList ??= new JintStatementList(Function);
                result = _bodyStatementList.Execute(context);
            }
        }

        argumentsInstance?.FunctionWasCalled();
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-async-functions-abstract-operations-async-function-start
    /// </summary>
    private static async ValueTask AsyncFunctionStart(EvaluationContext context, PromiseCapability promiseCapability, Func<EvaluationContext, ValueTask<Completion>> asyncFunctionBody)
    {
        var runningContext = context.Engine.ExecutionContext;
        var asyncContext = runningContext;
        await AsyncBlockStart(context, promiseCapability, asyncFunctionBody, asyncContext).ConfigureAwait(false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncblockstart
    /// </summary>
    private static async ValueTask AsyncBlockStart(
        EvaluationContext context,
        PromiseCapability promiseCapability,
        Func<EvaluationContext, ValueTask<Completion>> asyncBody,
        ExecutionContext asyncContext)
    {
        var runningContext = context.Engine.ExecutionContext;
        // Set the code evaluation state of asyncContext such that when evaluation is resumed for that execution contxt the following steps will be performed:

        Completion result;
        try
        {
            result = await asyncBody(context).ConfigureAwait(false);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(JsValue.Undefined, new[] { e.Error });
            return;
        }

        if (result.Type == CompletionType.Normal)
        {
            promiseCapability.Resolve.Call(JsValue.Undefined, new[] { JsValue.Undefined });
        }
        else if (result.Type == CompletionType.Return)
        {
            promiseCapability.Resolve.Call(JsValue.Undefined, new[] { result.Value });
        }
        else
        {
            promiseCapability.Reject.Call(JsValue.Undefined, new[] { result.Value });
        }

        /*
        4. Push asyncContext onto the execution context stack; asyncContext is now the running execution context.
        5. Resume the suspended evaluation of asyncContext. Let result be the value returned by the resumed computation.
        6. Assert: When we return here, asyncContext has already been removed from the execution context stack and runningContext is the currently running execution context.
        7. Assert: result is a normal completion with a value of unused. The possible sources of this value are Await or, if the async function doesn't await anything, step 3.g above.
        8. Return unused.
        */
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluategeneratorbody
    /// </summary>
    private Completion EvaluateGeneratorBody(
        EvaluationContext context,
        Function functionObject,
        JsValue[] argumentsList)
    {
        var engine = context.Engine;
        engine.FunctionDeclarationInstantiation(functionObject, argumentsList);
        var G = engine.Realm.Intrinsics.Function.OrdinaryCreateFromConstructor(
            functionObject,
            static intrinsics => intrinsics.GeneratorFunction.PrototypeObject.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new GeneratorInstance(engine));

        _bodyStatementList ??= new JintStatementList(Function);
        _bodyStatementList.Reset();
        G.GeneratorStart(_bodyStatementList);

        return new Completion(CompletionType.Return, G, Function.Body);
    }

    internal State Initialize()
    {
        var node = (Node) Function;
        var state = (State) (node.UserData ??= BuildState(Function));
        return state;
    }

    internal sealed class State
    {
        public bool HasRestParameter;
        public int Length;
        public Key[] ParameterNames = null!;
        public bool HasDuplicates;
        public bool IsSimpleParameterList;
        public bool HasParameterExpressions;
        public bool ArgumentsObjectNeeded;
        public List<Key>? VarNames;
        public LinkedList<FunctionDeclaration>? FunctionsToInitialize;
        public readonly HashSet<Key> FunctionNames = new();
        public DeclarationCache? LexicalDeclarations;
        public HashSet<Key>? ParameterBindings;
        public List<VariableValuePair>? VarsToInitialize;

        internal readonly record struct VariableValuePair(Key Name, JsValue? InitialValue);
    }

    internal static State BuildState(IFunction function)
    {
        var state = new State();

        ProcessParameters(function, state, out var hasArguments);

        var strict = function.IsStrict();
        var hoistingScope = HoistingScope.GetFunctionLevelDeclarations(strict, function);
        var functionDeclarations = hoistingScope._functionDeclarations;
        var lexicalNames = hoistingScope._lexicalNames;
        state.VarNames = hoistingScope._varNames;

        LinkedList<FunctionDeclaration>? functionsToInitialize = null;

        if (functionDeclarations != null)
        {
            functionsToInitialize = new LinkedList<FunctionDeclaration>();
            for (var i = functionDeclarations.Count - 1; i >= 0; i--)
            {
                var d = functionDeclarations[i];
                var fn = d.Id!.Name;
                if (state.FunctionNames.Add(fn))
                {
                    functionsToInitialize.AddFirst(d);
                }
            }
        }

        state.FunctionsToInitialize = functionsToInitialize;

        const string ParameterNameArguments = "arguments";

        state.ArgumentsObjectNeeded = true;
        var thisMode = strict ? FunctionThisMode.Strict : FunctionThisMode.Global;
        if (function.Type == NodeType.ArrowFunctionExpression)
        {
            thisMode = FunctionThisMode.Lexical;
        }

        if (thisMode == FunctionThisMode.Lexical)
        {
            state.ArgumentsObjectNeeded = false;
        }
        else if (hasArguments)
        {
            state.ArgumentsObjectNeeded = false;
        }
        else if (!state.HasParameterExpressions)
        {
            if (state.FunctionNames.Contains(ParameterNameArguments)
                || lexicalNames?.Contains(ParameterNameArguments) == true)
            {
                state.ArgumentsObjectNeeded = false;
            }
        }

        if (state.ArgumentsObjectNeeded)
        {
            // just one extra check...
            state.ArgumentsObjectNeeded = ArgumentsUsageAstVisitor.HasArgumentsReference(function);
        }

        var parameterBindings = new HashSet<Key>(state.ParameterNames);
        if (state.ArgumentsObjectNeeded)
        {
            parameterBindings.Add(KnownKeys.Arguments);
        }

        state.ParameterBindings = parameterBindings;

        var varsToInitialize = new List<State.VariableValuePair>();
        if (!state.HasParameterExpressions)
        {
            var instantiatedVarNames = state.VarNames != null
                ? new HashSet<Key>(state.ParameterBindings)
                : new HashSet<Key>();

            for (var i = 0; i < state.VarNames?.Count; i++)
            {
                var n = state.VarNames[i];
                if (instantiatedVarNames.Add(n))
                {
                    varsToInitialize.Add(new State.VariableValuePair
                    {
                        Name = n
                    });
                }
            }
        }
        else
        {
            var instantiatedVarNames = state.VarNames != null
                ? new HashSet<Key>(state.ParameterBindings)
                : null;

            for (var i = 0; i < state.VarNames?.Count; i++)
            {
                var n = state.VarNames[i];
                if (instantiatedVarNames!.Add(n))
                {
                    JsValue? initialValue = null;
                    if (!state.ParameterBindings.Contains(n) || state.FunctionNames.Contains(n))
                    {
                        initialValue = JsValue.Undefined;
                    }

                    varsToInitialize.Add(new State.VariableValuePair
                    {
                        Name = n,
                        InitialValue = initialValue
                    });
                }
            }
        }

        state.VarsToInitialize = varsToInitialize;

        if (hoistingScope._lexicalDeclarations != null)
        {
            state.LexicalDeclarations = DeclarationCacheBuilder.Build(hoistingScope._lexicalDeclarations);
        }

        return state;
    }

    private static void GetBoundNames(
        Node? parameter,
        List<Key> target,
        bool checkDuplicates,
        ref bool _hasRestParameter,
        ref bool _hasParameterExpressions,
        ref bool _hasDuplicates,
        ref bool hasArguments)
    {
        if (parameter is Identifier identifier)
        {
            _hasDuplicates |= checkDuplicates && target.Contains(identifier.Name);
            target.Add(identifier.Name);
            hasArguments |= string.Equals(identifier.Name, "arguments", StringComparison.Ordinal);
            return;
        }

        while (true)
        {
            if (parameter is RestElement restElement)
            {
                _hasRestParameter = true;
                _hasParameterExpressions = true;
                parameter = restElement.Argument;
                continue;
            }

            if (parameter is ArrayPattern arrayPattern)
            {
                _hasParameterExpressions = true;
                ref readonly var arrayPatternElements = ref arrayPattern.Elements;
                for (var i = 0; i < arrayPatternElements.Count; i++)
                {
                    var expression = arrayPatternElements[i];
                    GetBoundNames(
                        expression,
                        target,
                        checkDuplicates,
                        ref _hasRestParameter,
                        ref _hasParameterExpressions,
                        ref _hasDuplicates,
                        ref hasArguments);
                }
            }
            else if (parameter is ObjectPattern objectPattern)
            {
                _hasParameterExpressions = true;
                ref readonly var objectPatternProperties = ref objectPattern.Properties;
                for (var i = 0; i < objectPatternProperties.Count; i++)
                {
                    var property = objectPatternProperties[i];
                    if (property is AssignmentProperty p)
                    {
                        GetBoundNames(
                            p.Value,
                            target,
                            checkDuplicates,
                            ref _hasRestParameter,
                            ref _hasParameterExpressions,
                            ref _hasDuplicates,
                            ref hasArguments);
                    }
                    else
                    {
                        _hasRestParameter = true;
                        _hasParameterExpressions = true;
                        parameter = ((RestElement) property).Argument;
                        continue;
                    }
                }
            }
            else if (parameter is AssignmentPattern assignmentPattern)
            {
                _hasParameterExpressions = true;
                parameter = assignmentPattern.Left;
                continue;
            }

            break;
        }
    }

    private static void ProcessParameters(
        IFunction function,
        State state,
        out bool hasArguments)
    {
        hasArguments = false;
        state.IsSimpleParameterList = true;

        var countParameters = true;
        ref readonly var functionDeclarationParams = ref function.Params;
        var count = functionDeclarationParams.Count;
        var parameterNames = new List<Key>(count);
        for (var i = 0; i < count; i++)
        {
            var parameter = functionDeclarationParams[i];
            var type = parameter.Type;

            if (type == NodeType.Identifier)
            {
                var id = (Identifier) parameter;
                state.HasDuplicates |= parameterNames.Contains(id.Name);
                hasArguments = string.Equals(id.Name, "arguments", StringComparison.Ordinal);
                parameterNames.Add(id.Name);
            }
            else if (type != NodeType.Literal)
            {
                countParameters &= type != NodeType.AssignmentPattern;
                state.IsSimpleParameterList = false;
                GetBoundNames(
                    parameter,
                    parameterNames,
                    checkDuplicates: true,
                    ref state.HasRestParameter,
                    ref state.HasParameterExpressions,
                    ref state.HasDuplicates,
                    ref hasArguments);
            }

            if (countParameters && type is NodeType.Identifier or NodeType.ObjectPattern or NodeType.ArrayPattern)
            {
                state.Length++;
            }
        }

        state.ParameterNames = parameterNames.ToArray();
    }

    private static class ArgumentsUsageAstVisitor
    {
        public static bool HasArgumentsReference(IFunction function)
        {
            if (HasArgumentsReference(function.Body))
            {
                return true;
            }

            ref readonly var parameters = ref function.Params;
            for (var i = 0; i < parameters.Count; ++i)
            {
                if (HasArgumentsReference(parameters[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasArgumentsReference(Node node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                var childType = childNode.Type;
                if (childType == NodeType.Identifier)
                {
                    if (string.Equals(((Identifier) childNode).Name, "arguments", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                else if (childType != NodeType.FunctionDeclaration && !childNode.ChildNodes.IsEmpty())
                {
                    if (HasArgumentsReference(childNode))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
