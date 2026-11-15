using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintSpreadExpression : JintExpression
{
    internal readonly JintExpression _argument;
    private readonly string? _argumentName;

    public JintSpreadExpression(SpreadElement expression) : base(expression)
    {
        _argument = Build(expression.Argument);
        _argumentName = (expression.Argument as Identifier)?.Name;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
        return objectInstance;
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;

        GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
        return objectInstance;
    }

    internal void GetValueAndCheckIterator(EvaluationContext context, out JsValue instance, out IteratorInstance? iterator)
    {
        instance = _argument.GetValue(context);

        // If generator suspended during argument evaluation, don't try to get iterator
        if (context.IsSuspended())
        {
            iterator = null;
            return;
        }

        iterator = GetIterator(context.Engine.Realm, instance, iteratorMethod: null);
    }

    /// <summary>
    /// GetIterator(<paramref name="instance"/>, sync) (https://tc39.es/ecma262/#sec-getiterator) for what the argument
    /// evaluated to, taking <paramref name="iteratorMethod"/>, when it is not null, as what the one read of
    /// <c>@@iterator</c> found: the array lane's check makes that read and hands it over when it cannot read by index
    /// (sebastienros/jint#4426).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IteratorInstance GetIterator(Realm realm, JsValue instance, JsValue? iteratorMethod)
    {
        if (instance is null || !instance.TryGetIteratorFromRead(realm, iteratorMethod, out var iterator))
        {
            ThrowNotIterable(realm);
            return null!;
        }

        return iterator;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNotIterable(Realm realm) => Throw.TypeError(realm, _argumentName + " is not iterable");
}