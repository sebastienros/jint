using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

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
        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;
        var privateEnv = engine.ExecutionContext.PrivateEnvironment;

        ObjectInstance prototype = _function.Function.Async
            ? engine.Realm.Intrinsics.AsyncFunction.PrototypeObject
            : engine.Realm.Intrinsics.Function.PrototypeObject;

        var closure = engine.Realm.Intrinsics.Function.OrdinaryFunctionCreate(
            prototype,
            _function,
            FunctionThisMode.Lexical,
            env,
            privateEnv);

        if (_function.Name is null)
        {
            closure.SetFunctionName(JsString.Empty);
        }

        return closure;
    }
}
