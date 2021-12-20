using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintArrayExpression : JintExpression
    {
        private JintExpression[] _expressions;
        private bool _hasSpreads;

        public JintArrayExpression(ArrayExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            var node = (ArrayExpression) _expression;
            _expressions = new JintExpression[node.Elements.Count];
            for (var n = 0; n < _expressions.Length; n++)
            {
                var expr = node.Elements[n];
                if (expr != null)
                {
                    var expression = Build(engine, expr);
                    _expressions[n] = expression;
                    _hasSpreads |= expr.Type == Nodes.SpreadElement;
                }
            }

            // we get called from nested spread expansion in call
            _initialized = true;
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var a = engine.Realm.Intrinsics.Array.ConstructFast(_hasSpreads ? 0 : (uint) _expressions.Length);

            uint arrayIndexCounter = 0;
            foreach (var expr in _expressions)
            {
                if (expr == null)
                {
                    arrayIndexCounter++;
                    continue;
                }

                if (_hasSpreads && expr is JintSpreadExpression jse)
                {
                    jse.GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
                    // optimize for array
                    if (objectInstance is ArrayInstance ai)
                    {
                        var length = ai.GetLength();
                        var newLength = arrayIndexCounter + length;
                        a.EnsureCapacity(newLength);
                        a.CopyValues(ai, sourceStartIndex: 0, targetStartIndex: arrayIndexCounter, length);
                        arrayIndexCounter += length;
                        a.SetLength(newLength);
                    }
                    else
                    {
                        var protocol = new ArraySpreadProtocol(engine, a, iterator, arrayIndexCounter);
                        protocol.Execute();
                        arrayIndexCounter += protocol._addedCount;
                    }
                }
                else
                {
                    var value = expr.GetValue(context).Value;
                    a.SetIndexValue(arrayIndexCounter++, value, updateLength: false);
                }
            }

            if (_hasSpreads)
            {
                a.SetLength(arrayIndexCounter);
            }

            return NormalCompletion(a);
        }

        private sealed class ArraySpreadProtocol : IteratorProtocol
        {
            private readonly ArrayInstance _instance;
            internal long _index;
            internal uint _addedCount = 0;

            public ArraySpreadProtocol(
                Engine engine,
                ArrayInstance instance,
                IteratorInstance iterator,
                long startIndex) : base(engine, iterator, 0)
            {
                _instance = instance;
                _index = startIndex - 1;
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                _index++;
                _addedCount++;
                _instance.SetIndexValue((uint) _index, currentValue, updateLength: false);
            }
        }
    }
}