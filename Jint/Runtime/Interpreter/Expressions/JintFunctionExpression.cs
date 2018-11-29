using Esprima;
using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression<Expression>
    {
        private readonly IFunction _function;
        private readonly JintStatement _functionBody;
        private readonly string _name;

        public JintFunctionExpression(Engine engine, IFunction function) : base(engine, ArrowParameterPlaceHolder.Empty)
        {
            _function = function;
            _functionBody = JintStatement.Build(engine, function.Body);
            _name = !string.IsNullOrEmpty(function.Id?.Name) ? function.Id.Name : null;
        }

        public override Location Location => ExceptionHelper.ThrowNotImplementedException<Location>();

        protected override object EvaluateInternal()
        {
            var funcEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var envRec = (DeclarativeEnvironmentRecord) funcEnv._record;

            var closure = new ScriptFunctionInstance(
                _engine,
                _function,
                funcEnv,
                _function.Strict
            );

            if (_name != null)
            {
                envRec.CreateMutableBinding(_name, closure);
            }

            return closure;
        }
    }
}