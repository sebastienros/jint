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

    internal void Initialize(ReadOnlySpan<Expression> arguments)
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

            if (argument.Type == NodeType.Literal)
            {
                var literalValue = JintLiteralExpression.ConvertToJsValue((Literal) argument);
                if (literalValue is not null)
                {
                    _expressions[i] = literalValue;
                    continue;
                }
            }

            var expression = JintExpression.Build(argument);
            _expressions[i] = expression;
            _fullyCached = false;
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

    public JsValue[] ArgumentListEvaluation(EvaluationContext context, object key, out bool rented)
    {
        rented = false;
        if (_fullyCached)
        {
            return Unsafe.As<JsValue[]>(_expressions);
        }

        if (HasSpreads)
        {
            var args = ArgumentListEvaluationWithSpreadsResumable(context, key);
            return args.ToArray();
        }

        var suspendable = context.Engine.ExecutionContext.Suspendable;

        JsValue[] arguments;
        int startIndex;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(key, out ExpressionBufferSuspendData? suspendData))
        {
            // Resume: reuse the partially-filled buffer so already-evaluated
            // arguments (which may have observable side effects) are not re-evaluated.
            arguments = suspendData!.Buffer;
            startIndex = suspendData.NextIndex;
        }
        else
        {
            arguments = context.Engine._jsValueArrayPool.RentArray(_expressions.Length);
            startIndex = 0;
        }

        var nextIndex = BuildArguments(context, arguments, startIndex);

        if (context.IsSuspended())
        {
            // Buffer is kept alive in suspend data; caller must NOT return it to the pool.
            if (suspendable is not null)
            {
                var data = suspendable.Data.GetOrCreate<ExpressionBufferSuspendData>(key);
                data.Buffer = arguments;
                data.NextIndex = nextIndex;
            }

            return arguments;
        }

        // Completed normally — caller now owns the array and should return it to the pool.
        suspendable?.Data.Clear(key);
        rented = true;
        return arguments;
    }

    internal int BuildArguments(EvaluationContext context, JsValue[] targetArray, int startIndex = 0)
    {
        var expressions = _expressions;
        int i = startIndex;
        for (; (uint) i < (uint) expressions.Length; i++)
        {
            targetArray[i] = GetValue(context, expressions[i])!;

            // Check for generator suspension after each expression evaluation
            // This is needed because yield expressions return normally instead of throwing
            if (context.IsSuspended())
            {
                return i;
            }
        }

        return i;
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

    /// <summary>
    /// Iterates argument expressions, expanding spreads into <paramref name="target"/>.
    /// Returns the next expression index to resume at if suspension occurs; otherwise
    /// <c>_expressions.Length</c>. Suspension inside iterator <c>.next()</c> calls is
    /// not possible (spread protocol is synchronous); suspension can only happen at:
    /// - The <c>_argument</c> of a spread (e.g. <c>...(await fn())</c>) — captured before iteration starts.
    /// - A non-spread element's <c>GetValue</c> — captured before <c>target.Add</c>.
    /// </summary>
    internal int BuildArgumentsWithSpreads(EvaluationContext context, List<JsValue> target, int startIndex = 0)
    {
        var expressions = _expressions;
        int i = startIndex;
        for (; (uint) i < (uint) expressions.Length; i++)
        {
            var expression = expressions[i];
            if (expression is JintSpreadExpression jse)
            {
                jse.GetValueAndCheckIterator(context, out var objectInstance, out var iterator);

                // If generator suspended during spread expression evaluation, stop processing
                // The iterator will be null because we haven't started iteration yet.
                // Resume re-evaluates the spread (the await inside is cached).
                if (context.IsSuspended())
                {
                    return i;
                }

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
                var value = GetValue(context, expression)!;

                // Check for generator suspension BEFORE appending — so resume doesn't see
                // a leftover sentinel value at this index.
                if (context.IsSuspended())
                {
                    return i;
                }

                target.Add(value);
            }
        }

        return i;
    }

    /// <summary>
    /// Resume-aware variant of <see cref="BuildArgumentsWithSpreads"/> for use by callers
    /// that build an argument list with possible spread elements. The buffer list and
    /// next-expression index are preserved across suspension in
    /// <see cref="SpreadArgumentsSuspendData"/> keyed by <paramref name="key"/>.
    /// </summary>
    internal List<JsValue> ArgumentListEvaluationWithSpreadsResumable(EvaluationContext context, object key)
    {
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        List<JsValue> target;
        int startIndex;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(key, out SpreadArgumentsSuspendData? suspendData))
        {
            target = suspendData!.Target;
            startIndex = suspendData.NextExpressionIndex;
        }
        else
        {
            target = new List<JsValue>(_expressions.Length);
            startIndex = 0;
        }

        var nextIndex = BuildArgumentsWithSpreads(context, target, startIndex);

        if (context.IsSuspended())
        {
            if (suspendable is not null)
            {
                var data = suspendable.Data.GetOrCreate<SpreadArgumentsSuspendData>(key);
                data.Target = target;
                data.NextExpressionIndex = nextIndex;
            }
        }
        else
        {
            suspendable?.Data.Clear(key);
        }

        return target;
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
