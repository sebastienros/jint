using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintSwitchBlock
    {
        private readonly Engine _engine;
        private readonly List<SwitchCase> _switchBlock;
        private readonly JintSwitchCase[] _jintSwitchBlock;

        public JintSwitchBlock(Engine engine, List<SwitchCase> switchBlock)
        {
            _engine = engine;
            _switchBlock = switchBlock;
            _jintSwitchBlock = new JintSwitchCase[switchBlock.Count];
        }

        public Completion Execute(JsValue input)
        {
            JsValue v = Undefined.Instance;
            JintSwitchCase defaultCase = null;
            bool hit = false;

            for (var i = 0; i < _jintSwitchBlock.Length; i++)
            {
                var clause = _jintSwitchBlock[i] ?? (_jintSwitchBlock[i] = new JintSwitchCase(_engine, _switchBlock[i]));
                if (clause._test == null)
                {
                    defaultCase = clause;
                }
                else
                {
                    var clauseSelector = _engine.GetValue(clause._test.Evaluate(), true);
                    if (JintBinaryExpression.StrictlyEqual(clauseSelector, input))
                    {
                        hit = true;
                    }
                }

                if (hit && clause._consequent != null)
                {
                    var r = clause._consequent.Execute();
                    if (r.Type != CompletionType.Normal)
                    {
                        return r;
                    }

                    v = r.Value ?? Undefined.Instance;
                }
            }

            // do we need to execute the default case ?
            if (hit == false && defaultCase != null)
            {
                var r = defaultCase._consequent.Execute();
                if (r.Type != CompletionType.Normal)
                {
                    return r;
                }

                v = r.Value ?? Undefined.Instance;
            }

            return new Completion(CompletionType.Normal, v, null);
        }

        private sealed class JintSwitchCase
        {
            internal readonly JintStatementList _consequent;
            internal readonly JintExpression _test;

            public JintSwitchCase(Engine engine, SwitchCase switchCase)
            {
                if (switchCase.Consequent != null)
                {
                    _consequent = new JintStatementList(engine, null, switchCase.Consequent);
                }

                if (switchCase.Test != null)
                {
                    _test = JintExpression.Build(engine, switchCase.Test);
                }
            }
        }
    }
}