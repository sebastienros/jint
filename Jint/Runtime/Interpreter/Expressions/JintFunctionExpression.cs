using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintFunctionExpression(Engine engine, IFunction function)
            : base(ArrowParameterPlaceHolder.Empty)
        {
            _function = new JintFunctionDefinition(engine, function);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return GetValue(context);
        }

        public override Completion GetValue(EvaluationContext context)
        {
            var engine = context.Engine;
            var funcEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);

            var closure = new ScriptFunctionInstance(
                engine,
                _function,
                funcEnv,
                _function.ThisMode);

            closure.MakeConstructor();

            if (_function.Name != null)
            {
                funcEnv.CreateMutableBindingAndInitialize(_function.Name, canBeDeleted: false, closure);
            }

            return Completion.Normal(closure, _expression.Location);
        }
    }
}