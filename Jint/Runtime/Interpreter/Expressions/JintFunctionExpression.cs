using Esprima;
using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression
    {
        private readonly Engine _engine;
        private readonly IFunction _expression;
        private readonly JintStatement _functionBody;
        private readonly string _name;

        public JintFunctionExpression(Engine engine, IFunction expression)
        {
            _engine = engine;
            _expression = expression;
            _functionBody = JintStatement.Build(engine, expression.Body);
            _name = !string.IsNullOrEmpty(_expression.Id?.Name) ? _expression.Id.Name : null;
        }

        public override Location Location => ExceptionHelper.ThrowNotImplementedException<Location>();

        public override object Evaluate()
        {
            var funcEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var envRec = (DeclarativeEnvironmentRecord) funcEnv._record;

            var closure = new ScriptFunctionInstance(
                _engine,
                _expression,
                funcEnv,
                _expression.Strict
            );

            if (_name != null)
            {
                envRec.CreateMutableBinding(_name, closure);
            }

            return closure;
        }
    }
}