using Esprima.Ast;
using Jint.Native;
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
        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;
        var privateEnv = engine.ExecutionContext.PrivateEnvironment;

        var closure = engine.Realm.Intrinsics.Function.OrdinaryFunctionCreate(
            engine.Realm.Intrinsics.Function.PrototypeObject,
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
