using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintArrowFunctionExpression : JintExpression
{
    private readonly JintFunctionDefinition _function;

    public JintArrowFunctionExpression(ArrowFunctionExpression function) : base(function)
    {
        _function = new JintFunctionDefinition(function);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        return Build(context.Engine, _function);
    }

    private static ScriptFunction Build(Engine engine, JintFunctionDefinition function)
    {
        var functionName = function.Name ?? "";
        var closure = function.Function.Async
            ? InstantiateAsyncArrowFunctionExpression(engine, function, functionName)
            : InstantiateArrowFunctionExpression(engine, function, functionName);

        return closure;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiatearrowfunctionexpression
    /// </summary>
    private static ScriptFunction InstantiateArrowFunctionExpression(Engine engine, JintFunctionDefinition function, string name)
    {
        var runningExecutionContext = engine.ExecutionContext;
        var env = runningExecutionContext.LexicalEnvironment;
        var privateEnv = runningExecutionContext.PrivateEnvironment;

        var intrinsics = engine.Realm.Intrinsics;
        var closure = intrinsics.Function.OrdinaryFunctionCreate(
            intrinsics.Function.PrototypeObject,
            function,
            FunctionThisMode.Lexical,
            env,
            privateEnv
        );

        InheritStrictness(closure, in runningExecutionContext);
        closure.SetFunctionName(name);

        return closure;
    }

    /// <summary>
    /// An arrow function is strict whenever the code it appears in is
    /// (https://tc39.es/ecma262/#sec-strict-mode-code). The parser records that on the function's
    /// <c>FunctionBody</c>, which a concise (expression) body is not — for those the flag has to come
    /// from the context the arrow is instantiated in, which is exactly the code containing it.
    /// </summary>
    private static void InheritStrictness(ScriptFunction closure, in ExecutionContext runningExecutionContext)
    {
        closure._strict |= runningExecutionContext.Strict;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateasyncarrowfunctionexpression
    /// </summary>
    private static ScriptFunction InstantiateAsyncArrowFunctionExpression(Engine engine, JintFunctionDefinition function, string name)
    {
        var executionContext = engine.ExecutionContext;
        var env = executionContext.LexicalEnvironment;
        var privateEnv = executionContext.PrivateEnvironment;

        var intrinsics = engine.Realm.Intrinsics;
        var closure = intrinsics.Function.OrdinaryFunctionCreate(
            intrinsics.AsyncFunction.PrototypeObject,
            function,
            FunctionThisMode.Lexical,
            env,
            privateEnv
        );

        InheritStrictness(closure, in executionContext);
        closure.SetFunctionName(name);

        return closure;
    }
}
