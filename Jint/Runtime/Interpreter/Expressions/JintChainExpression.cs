using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-optional-chains
    /// </summary>
    internal sealed class JintChainExpression : JintExpression
    {
        private JintExpression _target;

        public JintChainExpression(Engine engine, ChainExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _target = Build(_engine, ((ChainExpression) _expression).Expression);
        }

        protected override object EvaluateInternal()
        {
            var baseReference = _target.GetValue();
            var baseValue = _engine.GetValue(baseReference);
            if (baseValue.IsNullOrUndefined())
            {
                return Undefined.Instance;
            }

            return baseValue;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-optional-chaining-chain-evaluation
        /// </summary>
        private object ChainEvaluation(JsValue baseValue, JsValue baseReference)
        {
            return null;
        }
    }
}