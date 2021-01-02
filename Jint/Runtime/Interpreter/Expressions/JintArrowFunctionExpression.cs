using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintArrowFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintArrowFunctionExpression(Engine engine, IFunction function)
            : base(engine, ArrowParameterPlaceHolder.Empty)
        {
            _function = new JintFunctionDefinition(engine, function);
        }

        protected override object EvaluateInternal()
        {
            var scope = _engine.ExecutionContext.LexicalEnvironment;

            var closure = new ScriptFunctionInstance(
                _engine,
                _function,
                scope,
                FunctionThisMode.Lexical,
                proto: _engine.Function.PrototypeObject);
            
            return closure;
        }
    }
}