using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// Optimizes constants values from expression array and only returns the actual JsValue in consecutive calls.
/// </summary>
internal sealed class ExpressionCache
{
    private object?[] _expressions = [];
    private bool _fullyCached;

    internal bool HasSpreads { get; private set; }

    internal void Initialize(EvaluationContext context, ReadOnlySpan<Expression> arguments)
    {
        if (arguments.Length == 0)
        {
            _fullyCached = true;
            _expressions = [];
            return;
        }

        _expressions = new object?[arguments.Length];
        _fullyCached = true;
        for (var i = 0; i < (uint) arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument is null)
            {
                _fullyCached = false;
                continue;
            }

            var expression = JintExpression.Build(argument);

            if (argument.Type == NodeType.Literal)
            {
                _expressions[i] = expression.GetValue(context).Clone();
                continue;
            }

            _expressions[i] = expression;
            _fullyCached &= argument.Type == NodeType.Literal;
            HasSpreads |= CanSpread(argument);

            if (argument.Type == NodeType.ArrayExpression)
            {
                ref readonly var elements = ref ((ArrayExpression) argument).Elements;
                foreach (var e in elements.AsSpan())
                {
                    HasSpreads |= CanSpread(e);
                }
            }
        }
    }

    public JsValue[] ArgumentListEvaluation(EvaluationContext context, out bool rented)
    {
        rented = false;
        if (_fullyCached)
        {
            return Unsafe.As<JsValue[]>(_expressions);
        }

        if (HasSpreads)
        {
            var args = new List<JsValue>(_expressions.Length);
            BuildArgumentsWithSpreads(context, args);
            return args.ToArray();
        }

        var arguments = context.Engine._jsValueArrayPool.RentArray(_expressions.Length);
        rented = true;

        BuildArguments(context, arguments);

        return arguments;
    }

    internal void BuildArguments(EvaluationContext context, JsValue[] targetArray)
    {
        var expressions = _expressions;
        for (uint i = 0; i < (uint) expressions.Length; i++)
        {
            targetArray[i] = GetValue(context, expressions[i])!;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue GetValue(EvaluationContext context, int index)
    {
        return GetValue(context, _expressions[index]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsValue GetValue(EvaluationContext context, object? value)
    {
        return value switch
        {
            JintExpression expression => expression.GetValue(context).Clone(),
            _ => (JsValue) value!,
        };
    }

    public bool IsAnonymousFunctionDefinition(int index)
    {
        var expressions = _expressions;
        return index < expressions.Length && (expressions[index] as JintExpression)?._expression.IsAnonymousFunctionDefinition() == true;
    }

    private static bool CanSpread(Node? e)
    {
        if (e is null)
        {
            return false;
        }

        return e.Type == NodeType.SpreadElement || e is AssignmentExpression { Right.Type: NodeType.SpreadElement };
    }

    internal JsValue[] DefaultSuperCallArgumentListEvaluation(EvaluationContext context)
    {
        // This branch behaves similarly to constructor(...args) { super(...args); }.
        // The most notable distinction is that while the aforementioned ECMAScript source text observably calls
        // the @@iterator method on %Array.prototype%, this function does not.

        var spreadExpression = (JintSpreadExpression) _expressions[0]!;
        var array = (JsArray) spreadExpression._argument.GetValue(context);
        var length = array.GetLength();
        var args = new List<JsValue>((int) length);
        for (uint j = 0; j < length; ++j)
        {
            array.TryGetValue(j, out var value);
            args.Add(value);
        }

        return args.ToArray();
    }

    internal void BuildArgumentsWithSpreads(EvaluationContext context, List<JsValue> target)
    {
        foreach (var expression in _expressions)
        {
            if (expression is JintSpreadExpression jse)
            {
                jse.GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
                // optimize for array unless someone has touched the iterator
                if (objectInstance is JsArray { HasOriginalIterator: true } ai)
                {
                    var length = ai.GetLength();
                    for (uint j = 0; j < length; ++j)
                    {
                        ai.TryGetValue(j, out var value);
                        target.Add(value);
                    }
                }
                else
                {
                    var protocol = new ArraySpreadProtocol(context.Engine, target, iterator!);
                    protocol.Execute();
                }
            }
            else
            {
                target.Add(GetValue(context, expression)!);
            }
        }
    }

    private sealed class ArraySpreadProtocol : IteratorProtocol
    {
        private readonly List<JsValue> _instance;

        public ArraySpreadProtocol(
            Engine engine,
            List<JsValue> instance,
            IteratorInstance iterator) : base(engine, iterator, 0)
        {
            _instance = instance;
        }

        protected override void ProcessItem(JsValue[] arguments, JsValue currentValue)
        {
            _instance.Add(currentValue);
        }
    }
}
