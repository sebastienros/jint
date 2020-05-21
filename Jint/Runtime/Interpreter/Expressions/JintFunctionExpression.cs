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

            var functionThisMode = _function._strict || _engine._isStrict
                ? FunctionInstance.FunctionThisMode.Strict 
                : FunctionInstance.FunctionThisMode.Global;

            var closure = new ScriptFunctionInstance(
                _engine,
                _function,
                funcEnv,
                functionThisMode);

            if (_function._name != null)
            {
                var envRec = (DeclarativeEnvironmentRecord) funcEnv._record;
                envRec.CreateMutableBinding(_function._name, canBeDeleted: false);
                envRec.InitializeBinding(_function._name, closure);
            }

            return closure;
        }
    }
}