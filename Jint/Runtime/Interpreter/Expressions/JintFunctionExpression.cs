using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintFunctionExpression(Engine engine, IFunction function)
            : base(engine, ArrowParameterPlaceHolder.Empty)
        {
            _function = new JintFunctionDefinition(engine, function);
        }

        protected override object EvaluateInternal()
        {
            var funcEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var envRec = (DeclarativeEnvironmentRecord) funcEnv._record;

            var closure = new ScriptFunctionInstance(
                _engine,
                _function,
                funcEnv,
                _function._strict);

            if (_function._name != null)
            {
                envRec.CreateMutableBinding(_function._name, closure);
            }

            return closure;
        }
    }
}