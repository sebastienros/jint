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

        public JintArrayExpression(Engine engine, ArrayExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var node = (ArrayExpression) _expression;
            _expressions = new JintExpression[node.Elements.Count];
            for (var n = 0; n < _expressions.Length; n++)
            {
                var expr = node.Elements[n];
                if (expr != null)
                {
                    var expression = Build(_engine, (Expression) expr);
                    _expressions[n] = expression;
                    _hasSpreads |= expression is JintSpreadExpression;
                }
            }

            // we get called from nested spread expansion in call
            _initialized = true;
        }

        protected override object EvaluateInternal()
        {
            var a = _engine.Array.ConstructFast(_hasSpreads ? 0 : (uint) _expressions.Length);
            var expressions = _expressions;

            uint arrayIndexCounter = 0;
            for (uint i = 0; i < (uint) expressions.Length; i++)
            {
                var expr = expressions[i];
                if (expr == null)
                {
                    arrayIndexCounter++;
                    continue;
                }

                if (_hasSpreads && expr is JintSpreadExpression jse)
                {
                    jse.GetValueAndCheckIterator(out var objectInstance, out var iterator);
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
                        var protocol = new ArraySpreadProtocol(_engine, a, iterator, arrayIndexCounter);
                        protocol.Execute();
                        arrayIndexCounter += protocol._addedCount;
                    }
                }
                else
                {
                    var value = expr.GetValue();
                    a.SetIndexValue(arrayIndexCounter++, value, updateLength: false);
                }
            }

            if (_hasSpreads)
            {
                a.SetLength(arrayIndexCounter);
            }

            return a;
        }

        private sealed class ArraySpreadProtocol : IteratorProtocol
        {
            private readonly ArrayInstance _instance;
            internal long _index;
            internal uint _addedCount = 0;

            public ArraySpreadProtocol(
                Engine engine,
                ArrayInstance instance,
                IIterator iterator,
                long startIndex) : base(engine, iterator, 0)
            {
                _instance = instance;
                _index = startIndex - 1;
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                _index++;
                _addedCount++;
                var jsValue = ExtractValueFromIteratorInstance(currentValue);

                _instance.SetIndexValue((uint) _index, jsValue, updateLength: false);
            }
        }
    }
}