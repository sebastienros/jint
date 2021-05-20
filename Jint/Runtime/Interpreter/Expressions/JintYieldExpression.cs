using Esprima.Ast;
using Jint.Native;
using Jint.Native.Generator;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintYieldExpression : JintExpression
    {
        public JintYieldExpression(Engine engine, YieldExpression expression) : base(engine, expression)
        {
        }

        protected override object EvaluateInternal()
        {
            var expression = (YieldExpression) _expression;
            var generatorKind = _engine.ExecutionContext.GetGeneratorKind();
            if (generatorKind == GeneratorKind.Async)
            {
                // TODO return ? AsyncGeneratorYield(undefined);
                ExceptionHelper.ThrowNotImplementedException();
            }

            var value = expression.Argument is not null 
                ? Build(_engine, expression.Argument).GetValue()
                : JsValue.Undefined;

            return GeneratorYield(new IteratorResult(_engine, value, JsBoolean.False));
        }


        /// <summary>
        /// https://tc39.es/ecma262/#sec-generatoryield
        /// </summary>
        private object GeneratorYield(JsValue iterNextObj)
        {
            var genContext = _engine.ExecutionContext;
            var generator = genContext.Generator;
            generator._generatorState = GeneratorState.SuspendedYield;
            _engine.LeaveExecutionContext();

            /*
            Set the code evaluation state of genContext such that when evaluation is resumed with a Completion resumptionValue the following steps will be performed:
                Return resumptionValue.
                NOTE: This returns to the evaluation of the YieldExpression that originally called this abstract operation.
            */

            return iterNextObj;
        }
    }
}