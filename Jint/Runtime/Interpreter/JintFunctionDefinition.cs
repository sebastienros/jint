using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.AsyncFunction;
using Jint.Native.AsyncGenerator;
using Jint.Native.Generator;
using Jint.Native.Promise;
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
    internal Completion EvaluateBody(EvaluationContext context, Function functionObject, JsCallArguments argumentsList)
    {
        Completion result;
        JsArguments? argumentsInstance = null;
        if (Function.Body is not FunctionBody)
        {
            // https://tc39.es/ecma262/#sec-runtime-semantics-evaluateconcisebody
            _bodyExpression ??= JintExpression.Build((Expression) Function.Body);
            if (Function.Async)
            {
                // local copies to prevent capturing closure created on top of method
                var function = functionObject;
                var jsValues = argumentsList;

                var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
                // Expression bodies don't have a statement list (used only for resumption)
                AsyncFunctionStart(context, promiseCapability, body: null, context =>
                {
                    context.Engine.FunctionDeclarationInstantiation(function, jsValues);
                    context.RunBeforeExecuteStatementChecks(Function.Body);
                    var jsValue = _bodyExpression.GetValue(context).Clone();

                    // Check for async suspension - if suspended, return early to allow resumption
                    if (context.IsSuspended())
                    {
                        return new Completion(CompletionType.Normal, jsValue, _bodyExpression._expression);
                    }

                    return new Completion(CompletionType.Return, jsValue, _bodyExpression._expression);
                });
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
            result = Function.Async
                ? EvaluateAsyncGeneratorBody(context, functionObject, argumentsList)
                : EvaluateGeneratorBody(context, functionObject, argumentsList);
        }
        else
        {
            if (Function.Async)
            {
                // local copies to prevent capturing closure created on top of method
                var function = functionObject;
                var arguments = argumentsList;

                var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
                // Each async function invocation needs its own JintStatementList to track its own position
                var bodyStatementList = new JintStatementList(Function);
                AsyncFunctionStart(context, promiseCapability, bodyStatementList, context =>
                {
                    context.Engine.FunctionDeclarationInstantiation(function, arguments);
                    return bodyStatementList.Execute(context);
                });
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
    private static void AsyncFunctionStart(
        EvaluationContext context,
        PromiseCapability promiseCapability,
        JintStatementList? body,
        Func<EvaluationContext, Completion> asyncFunctionBody)
    {
        var engine = context.Engine;
        var runningContext = engine.ExecutionContext;

        // Step 1-2: Create async function state tracking instance
        // This is an implementation detail not explicitly in spec, but needed for suspension/resumption
        var asyncInstance = new AsyncFunctionInstance
        {
            _state = AsyncFunctionState.Executing,
            _capability = promiseCapability,
            _body = body,
            _bodyFunction = asyncFunctionBody
        };

        // Step 3: "Let asyncContext be a copy of runningContext"
        // Since ExecutionContext is a readonly struct, UpdateAsyncFunction creates a new copy
        // with the AsyncFunction field set, achieving the spec's "copy" semantics.
        var asyncContext = runningContext.UpdateAsyncFunction(asyncInstance);

        // Store the context for resumption when awaited promises settle
        asyncInstance._savedContext = asyncContext;

        // Step 5: "Push asyncContext onto the execution context stack"
        // We leave the old context and push the new one (equivalent to spec's push operation)
        engine.LeaveExecutionContext();
        engine.EnterExecutionContext(asyncContext);

        // Step 6: "Resume the suspended evaluation of asyncContext"
        // Perform AsyncBlockStart to begin executing the async function body
        AsyncBlockStart(context, asyncInstance, asyncFunctionBody);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncblockstart
    /// </summary>
    private static void AsyncBlockStart(
        EvaluationContext context,
        AsyncFunctionInstance asyncInstance,
        Func<EvaluationContext, Completion> asyncBody)
    {
        var engine = context.Engine;

        Completion result;
        try
        {
            result = asyncBody(context);
        }
        catch (JavaScriptException e)
        {
            asyncInstance._state = AsyncFunctionState.Completed;
            asyncInstance._capability.Reject.Call(JsValue.Undefined, e.Error);
            return;
        }

        // Check if we suspended at an await
        if (asyncInstance._state == AsyncFunctionState.SuspendedAwait)
        {
            // Suspended - promise reaction will resume execution later
            return;
        }

        // Completed - resolve or reject the async function's return promise
        asyncInstance._state = AsyncFunctionState.Completed;

        if (result.Type == CompletionType.Normal)
        {
            asyncInstance._capability.Resolve.Call(JsValue.Undefined, JsValue.Undefined);
        }
        else if (result.Type == CompletionType.Return)
        {
            asyncInstance._capability.Resolve.Call(JsValue.Undefined, result.Value);
        }
        else
        {
            asyncInstance._capability.Reject.Call(JsValue.Undefined, result.Value);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluategeneratorbody
    /// </summary>
    private Completion EvaluateGeneratorBody(
        EvaluationContext context,
        Function functionObject,
        JsCallArguments argumentsList)
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluateasyncgeneratorbody
    /// </summary>
    private Completion EvaluateAsyncGeneratorBody(
        EvaluationContext context,
        Function functionObject,
        JsCallArguments argumentsList)
    {
        var engine = context.Engine;
        engine.FunctionDeclarationInstantiation(functionObject, argumentsList);
        var G = engine.Realm.Intrinsics.Function.OrdinaryCreateFromConstructor(
            functionObject,
            static intrinsics => intrinsics.AsyncGeneratorFunction.PrototypeObject.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new AsyncGeneratorInstance(engine));

        _bodyStatementList ??= new JintStatementList(Function);
        _bodyStatementList.Reset();
        G.AsyncGeneratorStart(_bodyStatementList);

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
        public bool RequiresInputArgumentsOwnership;
        public List<Key>? VarNames;
        public LinkedList<FunctionDeclaration>? FunctionsToInitialize;
        public readonly HashSet<Key> FunctionNames = new();
        public DeclarationCache? LexicalDeclarations;
        public HashSet<Key>? ParameterBindings;
        public List<VariableValuePair>? VarsToInitialize;
        public bool NeedsEvalContext;

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

        state.ArgumentsObjectNeeded = true;
        var thisMode = strict ? FunctionThisMode.Strict : FunctionThisMode.Global;
        if (function.Type == NodeType.ArrowFunctionExpression)
        {
            thisMode = FunctionThisMode.Lexical;
        }

        if (thisMode == FunctionThisMode.Lexical || hasArguments)
        {
            state.ArgumentsObjectNeeded = false;
        }
        else if (!state.HasParameterExpressions)
        {
            if (state.FunctionNames.Contains(KnownKeys.Arguments) || lexicalNames?.Contains(KnownKeys.Arguments.Name) == true)
            {
                state.ArgumentsObjectNeeded = false;
            }
        }

        if (state.ArgumentsObjectNeeded)
        {
            // just one extra check...
            state.ArgumentsObjectNeeded = ArgumentsUsageAstVisitor.HasArgumentsReference(function);
        }

        state.NeedsEvalContext = !strict;
        if (state.NeedsEvalContext)
        {
            // yet another extra check
            state.NeedsEvalContext = EvalContextAstVisitor.HasEvalOrDebugger(function);
        }

        var parameterBindings = new HashSet<Key>(state.ParameterNames);
        if (state.ArgumentsObjectNeeded)
        {
            parameterBindings.Add(KnownKeys.Arguments);
        }

        if (function.Type == NodeType.ArrowFunctionExpression)
        {
            state.RequiresInputArgumentsOwnership = state.ArgumentsObjectNeeded ||
                (function.Async && ArgumentsUsageAstVisitor.HasArgumentsReference(function));
        }
        else
        {
            state.RequiresInputArgumentsOwnership = state.ArgumentsObjectNeeded &&
                (function.Async || function.Generator);
        }

        state.ParameterBindings = parameterBindings;

        var varsToInitialize = new List<State.VariableValuePair>();
        if (!state.HasParameterExpressions)
        {
            var instantiatedVarNames = state.VarNames != null
                ? new HashSet<Key>(state.ParameterBindings)
                : new HashSet<Key>();

            // Add function names first (they take precedence over var declarations with same name)
            foreach (var fn in state.FunctionNames)
            {
                if (instantiatedVarNames.Add(fn))
                {
                    varsToInitialize.Add(new State.VariableValuePair(Name: fn, InitialValue: null));
                }
            }

            for (var i = 0; i < state.VarNames?.Count; i++)
            {
                var n = state.VarNames[i];
                if (instantiatedVarNames.Add(n))
                {
                    varsToInitialize.Add(new State.VariableValuePair(Name: n, InitialValue: null));
                }
            }
        }
        else
        {
            var instantiatedVarNames = state.VarNames != null
                ? new HashSet<Key>(state.ParameterBindings)
                : null;

            // Add function names first (they take precedence over var declarations with same name)
            foreach (var fn in state.FunctionNames)
            {
                if (instantiatedVarNames?.Add(fn) != false)
                {
                    instantiatedVarNames ??= new HashSet<Key>();
                    instantiatedVarNames.Add(fn);
                    JsValue? initialValue = null;
                    if (!state.ParameterBindings.Contains(fn))
                    {
                        initialValue = JsValue.Undefined;
                    }
                    varsToInitialize.Add(new State.VariableValuePair(Name: fn, InitialValue: initialValue));
                }
            }

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

                    varsToInitialize.Add(new State.VariableValuePair(Name: n, InitialValue: initialValue));
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
        Node parameter,
        List<Key> target,
        ref bool hasRestParameter,
        ref bool hasParameterExpressions,
        ref bool hasDuplicates,
        ref bool hasArguments)
    {
Start:
        if (parameter.Type == NodeType.Identifier)
        {
            var key = (Key) ((Identifier) parameter).Name;
            hasDuplicates |= target.Contains(key);
            target.Add(key);
            hasArguments |= key == KnownKeys.Arguments;
            return;
        }

        while (true)
        {
            if (parameter.Type == NodeType.RestElement)
            {
                hasRestParameter = true;
                parameter = ((RestElement) parameter).Argument;
                continue;
            }

            if (parameter.Type == NodeType.ArrayPattern)
            {
                foreach (var element in ((ArrayPattern) parameter).Elements.AsSpan())
                {
                    if (element is null)
                    {
                        continue;
                    }

                    if (element.Type == NodeType.RestElement)
                    {
                        hasRestParameter = true;
                        parameter = ((RestElement) element).Argument;
                        goto Start;
                    }

                    GetBoundNames(
                        element,
                        target,
                        ref hasRestParameter,
                        ref hasParameterExpressions,
                        ref hasDuplicates,
                        ref hasArguments);
                }
            }
            else if (parameter.Type == NodeType.ObjectPattern)
            {
                foreach (var property in ((ObjectPattern) parameter).Properties.AsSpan())
                {
                    if (property.Type == NodeType.RestElement)
                    {
                        hasRestParameter = true;
                        parameter = ((RestElement) property).Argument;
                        goto Start;
                    }

                    GetBoundNames(
                        ((AssignmentProperty) property).Value,
                        target,
                        ref hasRestParameter,
                        ref hasParameterExpressions,
                        ref hasDuplicates,
                        ref hasArguments);
                }
            }
            else if (parameter.Type == NodeType.AssignmentPattern)
            {
                var assignmentPattern = (AssignmentPattern) parameter;
                hasParameterExpressions |= ExpressionAstVisitor.HasExpression(assignmentPattern.ChildNodes);
                parameter = assignmentPattern.Left;

                // need to goto Start so Identifier case is handled
                goto Start;
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
        foreach (var parameter in function.Params.AsSpan())
        {
            var type = parameter.Type;

            if (type == NodeType.Identifier)
            {
                var key = (Key) ((Identifier) parameter).Name;
                state.HasDuplicates |= parameterNames.Contains(key);
                hasArguments |= key == KnownKeys.Arguments;
                parameterNames.Add(key);
            }
            else if (type != NodeType.Literal)
            {
                countParameters &= type != NodeType.AssignmentPattern;
                state.IsSimpleParameterList = false;
                GetBoundNames(
                    parameter,
                    parameterNames,
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

            foreach (var parameter in function.Params.AsSpan())
            {
                if (HasArgumentsReference(parameter))
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

    private static class EvalContextAstVisitor
    {
        public static bool HasEvalOrDebugger(IFunction function)
        {
            if (HasEvalOrDebugger(function.Body))
            {
                return true;
            }

            return false;
        }

        private static bool HasEvalOrDebugger(Node node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                var childType = childNode.Type;
                if (childType == NodeType.DebuggerStatement)
                {
                    return true;
                }

                if (childType == NodeType.CallExpression)
                {
                    if (((CallExpression) childNode).Callee is Identifier identifier && identifier.Name.Equals("eval", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                else if (childType != NodeType.FunctionDeclaration && !childNode.ChildNodes.IsEmpty())
                {
                    if (HasEvalOrDebugger(childNode))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private static class ExpressionAstVisitor
    {
        internal static bool HasExpression(ChildNodes nodes)
        {
            foreach (var childNode in nodes)
            {
                switch (childNode.Type)
                {
                    case NodeType.ArrowFunctionExpression:
                    case NodeType.FunctionExpression:
                    case NodeType.CallExpression:
                    case NodeType.AssignmentExpression:
                        return true;
                    case NodeType.Identifier:
                    case NodeType.Literal:
                        continue;
                    default:
                        if (!childNode.ChildNodes.IsEmpty())
                        {
                            if (HasExpression(childNode.ChildNodes))
                            {
                                return true;
                            }
                        }

                        break;
                }
            }

            return false;
        }
    }
}
