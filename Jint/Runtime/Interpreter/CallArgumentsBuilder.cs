using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter;

/// <summary>
/// Builds efficient array indexer free Arguments instance builders.
/// </summary>
internal abstract class CallArgumentsBuilder
{
    private static readonly EmptyCallArgumentsBuilder _empty = new();

    internal abstract Arguments Build(EvaluationContext context);

    internal static CallArgumentsBuilder GetArgumentsBuilder(JintExpression[] expressions, bool? hasKnownSpreads = null)
    {
        if (expressions.Length == 0)
        {
            return _empty;
        }

        var hasSpreads = false;
        if (hasKnownSpreads is null)
        {
            foreach (var item in expressions)
            {
                if (item._expression.Type == Nodes.SpreadElement)
                {
                    hasSpreads = true;
                    break;
                }
            }
        }

        if (hasKnownSpreads.GetValueOrDefault(hasSpreads))
        {
            return new SpreadArgumentsBuilder(expressions);
        }

        return expressions.Length switch
        {
            1 => new Call1Builder(expressions[0]),
            2 => new Call2Builder(expressions[0], expressions[1]),
            3 => new Call3Builder(expressions[0], expressions[1], expressions[2]),
            4 => new Call4Builder(expressions[0], expressions[1], expressions[2], expressions[3]),
            _ => new CallManyBuilder(expressions)
        };
    }

    private sealed class EmptyCallArgumentsBuilder : CallArgumentsBuilder
    {
        internal override Arguments Build(EvaluationContext context) => Arguments.Empty;
    }

    private sealed class Call1Builder : CallArgumentsBuilder
    {
        private readonly JintExpression _expression;

        public Call1Builder(JintExpression expression)
        {
            _expression = expression;
        }

        internal override Arguments Build(EvaluationContext context)
        {
            return new Arguments(_expression.GetValue(context).Value.Clone());
        }
    }

    private sealed class Call2Builder : CallArgumentsBuilder
    {
        private readonly JintExpression _expression1;
        private readonly JintExpression _expression2;

        public Call2Builder(JintExpression expression1, JintExpression expression2)
        {
            _expression1 = expression1;
            _expression2 = expression2;
        }

        internal override Arguments Build(EvaluationContext context)
        {
            return new Arguments(_expression1.GetValue(context).Value.Clone(), _expression2.GetValue(context).Value.Clone());
        }
    }

    private sealed class Call3Builder : CallArgumentsBuilder
    {
        private readonly JintExpression _expression1;
        private readonly JintExpression _expression2;
        private readonly JintExpression _expression3;

        public Call3Builder(JintExpression expression1, JintExpression expression2, JintExpression expression3)
        {
            _expression1 = expression1;
            _expression2 = expression2;
            _expression3 = expression3;
        }

        internal override Arguments Build(EvaluationContext context)
        {
            return new Arguments(
                _expression1.GetValue(context).Value.Clone(),
                _expression2.GetValue(context).Value.Clone(),
                _expression3.GetValue(context).Value.Clone());
        }
    }

    private sealed class Call4Builder : CallArgumentsBuilder
    {
        private readonly JintExpression _expression1;
        private readonly JintExpression _expression2;
        private readonly JintExpression _expression3;
        private readonly JintExpression _expression4;

        public Call4Builder(JintExpression expression1, JintExpression expression2, JintExpression expression3, JintExpression expression4)
        {
            _expression1 = expression1;
            _expression2 = expression2;
            _expression3 = expression3;
            _expression4 = expression4;
        }

        internal override Arguments Build(EvaluationContext context)
        {
            return new Arguments(
                _expression1.GetValue(context).Value.Clone(),
                _expression2.GetValue(context).Value.Clone(),
                _expression3.GetValue(context).Value.Clone(),
                _expression4.GetValue(context).Value.Clone());
        }
    }

    private sealed class CallManyBuilder : CallArgumentsBuilder
    {
        private readonly JintExpression[] _expressions;

        public CallManyBuilder(JintExpression[] expressions)
        {
            _expressions = expressions;
        }

        internal override Arguments Build(EvaluationContext context)
        {
            var expressions = _expressions;
            var targetArray = new JsValue[expressions.Length];
            for (var i = 0; i < targetArray.Length; i++)
            {
                targetArray[i] = expressions[i].GetValue(context).Value.Clone();
            }

            return new Arguments(targetArray, targetArray.Length);
        }
    }

    private sealed class SpreadArgumentsBuilder : CallArgumentsBuilder
    {
        private readonly JintExpression[] _expressions;

        public SpreadArgumentsBuilder(JintExpression[] expressions)
        {
            _expressions = expressions;
        }

        internal override Arguments Build(EvaluationContext context)
        {
            var expressions = _expressions;
            var args = new System.Collections.Generic.List<JsValue>(expressions.Length);
            for (var i = 0; i < expressions.Length; i++)
            {
                var jintExpression = expressions[i];
                if (jintExpression is JintSpreadExpression jse)
                {
                    jse.GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
                    // optimize for array unless someone has touched the iterator
                    if (objectInstance is ArrayInstance ai && ai.HasOriginalIterator)
                    {
                        var length = ai.GetLength();
                        for (uint j = 0; j < length; ++j)
                        {
                            if (ai.TryGetValue(j, out var value))
                            {
                                args.Add(value);
                            }
                        }
                    }
                    else
                    {
                        var protocol = new ArraySpreadProtocol(context.Engine, args, iterator);
                        protocol.Execute();
                    }
                }
                else
                {
                    var completion = jintExpression.GetValue(context);
                    args.Add(completion.Value!.Clone());
                }
            }

            return new Arguments(args.ToArray(), args.Count);
        }
    }

    private sealed class ArraySpreadProtocol : IteratorProtocol
    {
        private readonly System.Collections.Generic.List<JsValue> _instance;

        public ArraySpreadProtocol(
            Engine engine,
            System.Collections.Generic.List<JsValue> instance,
            IteratorInstance iterator) : base(engine, iterator, 0)
        {
            _instance = instance;
        }

        protected override void ProcessItem(JsValue currentValue)
        {
            _instance.Add(currentValue);
        }
    }
}
