using Jint.Native.Function;

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

        closure.SetFunctionName(name);

        return closure;
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

        closure.SetFunctionName(name);

        return closure;
    }
}
