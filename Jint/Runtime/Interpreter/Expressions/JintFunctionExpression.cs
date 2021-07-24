using Esprima.Ast;
using Jint.Native;
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
            return GetValue();
        }

        public override JsValue GetValue()
        {
            var funcEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);

            var functionThisMode = _function.Strict || _engine._isStrict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            var closure = new ScriptFunctionInstance(
                _engine,
                _function,
                funcEnv,
                functionThisMode);

            closure.MakeConstructor();

            if (_function.Name != null)
            {
                funcEnv.CreateMutableBindingAndInitialize(_function.Name, canBeDeleted: false, closure);
            }

            return closure;
        }
    }
}