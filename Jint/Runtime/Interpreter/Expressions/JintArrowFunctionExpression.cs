using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintArrowFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintArrowFunctionExpression(Engine engine, ArrowFunctionExpression function)
            : base(ArrowParameterPlaceHolder.Empty)
        {
            _function = new JintFunctionDefinition(engine, function);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var scope = engine.ExecutionContext.LexicalEnvironment;

            var closure = new ScriptFunctionInstance(
                engine,
                _function,
                scope,
                FunctionThisMode.Lexical,
                proto: engine.Realm.Intrinsics.Function.PrototypeObject);

            return NormalCompletion(closure);
        }
    }
}