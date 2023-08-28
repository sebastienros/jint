using Esprima.Ast;
using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintArrayExpression : JintExpression
    {
        private JintExpression?[] _expressions = Array.Empty<JintExpression?>();
        private bool _hasSpreads;

        private JintArrayExpression(ArrayExpression expression) : base(expression)
        {
            _initialized = false;
        }

        public static JintExpression Build(ArrayExpression expression)
        {
            return expression.Elements.Count == 0
                ? JintEmptyArrayExpression.Instance
                : new JintArrayExpression(expression);
        }

        protected override void Initialize(EvaluationContext context)
        {
            ref readonly var elements = ref ((ArrayExpression) _expression).Elements;
            var expressions = _expressions = new JintExpression[((ArrayExpression) _expression).Elements.Count];
            for (var n = 0; n < expressions.Length; n++)
            {
                var expr = elements[n];
                if (expr != null)
                {
                    var expression = Build(expr);
                    expressions[n] = expression;
                    _hasSpreads |= expr.Type == Nodes.SpreadElement;
                }
            }

            // we get called from nested spread expansion in call
            _initialized = true;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var a = engine.Realm.Intrinsics.Array.ArrayCreate(_hasSpreads ? 0 : (uint) _expressions.Length);

            uint arrayIndexCounter = 0;
            foreach (var expr in _expressions)
            {
                if (expr == null)
                {
                    a.SetIndexValue(arrayIndexCounter++, null, updateLength: false);
                }
                else if (_hasSpreads && expr is JintSpreadExpression jse)
                {
                    jse.GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
                    // optimize for array
                    if (objectInstance is JsArray ai)
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
                        var protocol = new ArraySpreadProtocol(engine, a, iterator!, arrayIndexCounter);
                        protocol.Execute();
                        arrayIndexCounter += protocol._addedCount;
                    }
                }
                else
                {
                    var value = expr.GetValue(context);
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
            private readonly JsArray _instance;
            private long _index;
            internal uint _addedCount;

            public ArraySpreadProtocol(
                Engine engine,
                JsArray instance,
                IteratorInstance iterator,
                long startIndex) : base(engine, iterator, 0)
            {
                _instance = instance;
                _index = startIndex - 1;
            }

            protected override void ProcessItem(JsValue[] arguments, JsValue currentValue)
            {
                _index++;
                _addedCount++;
                _instance.SetIndexValue((uint) _index, currentValue, updateLength: false);
            }
        }

        internal sealed class JintEmptyArrayExpression : JintExpression
        {
            public static JintEmptyArrayExpression Instance = new(new ArrayExpression(NodeList.Create(System.Linq.Enumerable.Empty<Expression?>())));

            private JintEmptyArrayExpression(Expression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                return new JsArray(context.Engine, Array.Empty<JsValue>());
            }

            public override JsValue GetValue(EvaluationContext context)
            {
                return new JsArray(context.Engine, Array.Empty<JsValue>());
            }
        }
    }
}
