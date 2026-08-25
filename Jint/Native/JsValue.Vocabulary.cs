using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.Native;

// The vocabulary every host types: what a value is, what is in it, and whether reading it worked. These are
// instance members rather than extension methods because Jint owns JsValue, so a host that has imported the
// namespace the type lives in should see them by dotting it. Which members are promoted and which stay on
// JsValueExtensions - in this same namespace - is the rule stated there.
public abstract partial class JsValue
{
    /// <summary>
    /// Returns whether this value is <c>undefined</c>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsUndefined() => _type == InternalTypes.Undefined;

    /// <summary>
    /// Returns whether this value is <c>null</c>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNull() => _type == InternalTypes.Null;

    /// <summary>
    /// Returns whether this value is a string primitive, which a <c>String</c> wrapper object is not.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsString() => (_type & InternalTypes.String) != InternalTypes.Empty;

    /// <summary>
    /// Returns whether this value is a number primitive, which a <c>Number</c> wrapper object is not.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNumber() => (_type & (InternalTypes.Number | InternalTypes.Integer)) != InternalTypes.Empty;

    /// <summary>
    /// Returns whether this value is a boolean primitive, which a <c>Boolean</c> wrapper object is not.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBoolean() => _type == InternalTypes.Boolean;

    /// <summary>
    /// Returns whether this value is a <c>BigInt</c>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBigInt() => (_type & InternalTypes.BigInt) != InternalTypes.Empty;

    /// <summary>
    /// Returns whether this value is a symbol.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSymbol() => _type == InternalTypes.Symbol;

    /// <summary>
    /// Returns whether this value is an object rather than a primitive.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsObject() => (_type & InternalTypes.Object) != InternalTypes.Empty;

    /// <summary>
    /// Returns whether this value is a <see cref="JsArray"/>.
    /// </summary>
    /// <remarks>
    /// This is the concrete-type question, so that it and <see cref="AsArray"/> agree. A <c>Proxy</c> whose
    /// target is an array answers <see langword="false"/>, as does <c>Array.prototype</c>, which the
    /// specification's own <c>IsArray</c> counts; script's <c>Array.isArray</c> is the one that follows a
    /// proxy.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsArray() => this is JsArray;

    /// <summary>
    /// Returns whether this value is a <c>Date</c>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDate() => this is JsDate;

    /// <summary>
    /// Returns whether this value is a promise.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPromise() => this is JsPromise;

    /// <summary>
    /// Returns whether this value can be called, which every function object and a <c>Proxy</c> over one can.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCallable() => HasCall;

    /// <summary>
    /// Returns whether this value is a regular expression, per
    /// <see href="https://tc39.es/ecma262/#sec-isregexp">IsRegExp</see>.
    /// </summary>
    /// <remarks>
    /// Unlike every other predicate here this one is not a type test: it reads <c>Symbol.match</c> off the
    /// value first, so an ordinary object carrying a truthy one answers <see langword="true"/>, and an
    /// accessor there runs script.
    /// </remarks>
    [Pure]
    public bool IsRegExp()
    {
        if (this is not ObjectInstance oi)
        {
            return false;
        }

        var matcher = oi.Get(GlobalSymbolRegistry.Match);
        if (!matcher.IsUndefined())
        {
            return TypeConverter.ToBoolean(matcher);
        }

        return this is JsRegExp;
    }

    /// <summary>
    /// Returns the CLR string behind a string primitive.
    /// </summary>
    /// <returns>The string this value holds.</returns>
    /// <exception cref="ArgumentException">This value is not a string primitive.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string AsString()
    {
        if (!IsString())
        {
            ThrowWrongType("string");
        }

        return ToString();
    }

    /// <summary>
    /// Returns the CLR double behind a number primitive.
    /// </summary>
    /// <returns>The number this value holds.</returns>
    /// <exception cref="ArgumentException">This value is not a number primitive.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double AsNumber()
    {
        if (!IsNumber())
        {
            ThrowWrongType("number");
        }

        Debug.Assert(this is JsNumber);
        return Unsafe.As<JsNumber>(this)._value;
    }

    /// <summary>
    /// Returns the CLR bool behind a boolean primitive.
    /// </summary>
    /// <returns>The boolean this value holds.</returns>
    /// <exception cref="ArgumentException">This value is not a boolean primitive.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AsBoolean()
    {
        if (_type != InternalTypes.Boolean)
        {
            ThrowWrongType("boolean");
        }

        return ((JsBoolean) this)._value;
    }

    /// <summary>
    /// Returns this value as the <see cref="ObjectInstance"/> it already is.
    /// </summary>
    /// <returns>This value, typed as an object.</returns>
    /// <exception cref="ArgumentException">This value is a primitive.</exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ObjectInstance AsObject()
    {
        if (!IsObject())
        {
            Throw.ArgumentException("The value is not an object");
        }

        return (ObjectInstance) this;
    }

    /// <summary>
    /// Returns this value as the <see cref="JsArray"/> it already is.
    /// </summary>
    /// <returns>This value, typed as an array.</returns>
    /// <exception cref="ArgumentException">This value is not a <see cref="JsArray"/>.</exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsArray AsArray()
    {
        if (!IsArray())
        {
            Throw.ArgumentException("The value is not an array");
        }

        return (JsArray) this;
    }

    /// <summary>
    /// Returns whether this value is a string primitive, handing back its CLR string when it is.
    /// </summary>
    /// <param name="value">The string this value holds, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when this value is a string primitive.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetString([NotNullWhen(true)] out string? value)
    {
        if (!IsString())
        {
            value = null;
            return false;
        }

        value = ToString();
        return true;
    }

    /// <summary>
    /// Returns whether this value is a number primitive, handing back its CLR double when it is.
    /// </summary>
    /// <param name="value">The number this value holds, or zero.</param>
    /// <returns><see langword="true"/> when this value is a number primitive.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNumber(out double value)
    {
        if (!IsNumber())
        {
            value = 0;
            return false;
        }

        Debug.Assert(this is JsNumber);
        value = Unsafe.As<JsNumber>(this)._value;
        return true;
    }

    /// <summary>
    /// Returns whether this value is a boolean primitive, handing back its CLR bool when it is.
    /// </summary>
    /// <param name="value">The boolean this value holds, or <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when this value is a boolean primitive.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetBoolean(out bool value)
    {
        if (_type != InternalTypes.Boolean)
        {
            value = false;
            return false;
        }

        value = ((JsBoolean) this)._value;
        return true;
    }

    /// <summary>
    /// Returns whether this value is an object, handing it back typed as one when it is.
    /// </summary>
    /// <param name="value">This value typed as an object, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when this value is an object rather than a primitive.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetObject([NotNullWhen(true)] out ObjectInstance? value)
    {
        value = this as ObjectInstance;
        return value is not null;
    }

    /// <summary>
    /// Returns whether this value is a <see cref="JsArray"/>, handing it back typed as one when it is.
    /// </summary>
    /// <param name="value">This value typed as an array, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when this value is a <see cref="JsArray"/>.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetArray([NotNullWhen(true)] out JsArray? value)
    {
        value = this as JsArray;
        return value is not null;
    }

    /// <summary>
    /// Returns what a settled promise fulfilled with, or this value unchanged when it is not a promise.
    /// </summary>
    /// <returns>The fulfilment value, or this value.</returns>
    /// <exception cref="PromiseRejectedException">The promise rejected, or the bound elapsed.</exception>
    /// <remarks>
    /// A pending promise is driven to settlement on the calling thread, bounded by the engine's own
    /// <c>Options.Constraints.PromiseTimeout</c>, which defaults to ten seconds. Use
    /// <see cref="UnwrapIfPromise(TimeSpan)"/> for a different bound, or
    /// <see cref="UnwrapIfPromiseAsync"/> not to block at all.
    /// </remarks>
    public JsValue UnwrapIfPromise() => UnwrapIfPromiseCore(timeout: null, CancellationToken.None);

    /// <summary>
    /// Returns what a settled promise fulfilled with, waiting at most <paramref name="timeout"/>, or this
    /// value unchanged when it is not a promise.
    /// </summary>
    /// <param name="timeout">How long to drive the event loop before giving up.</param>
    /// <returns>The fulfilment value, or this value.</returns>
    /// <exception cref="PromiseRejectedException">The promise rejected, or the bound elapsed.</exception>
    public JsValue UnwrapIfPromise(TimeSpan timeout) => UnwrapIfPromiseCore(timeout, CancellationToken.None);

    /// <summary>
    /// Returns what a settled promise fulfilled with, waiting until <paramref name="cancellationToken"/> is
    /// signalled, or this value unchanged when it is not a promise.
    /// </summary>
    /// <param name="cancellationToken">The token to observe while waiting.</param>
    /// <returns>The fulfilment value, or this value.</returns>
    /// <exception cref="PromiseRejectedException">The promise rejected.</exception>
    public JsValue UnwrapIfPromise(CancellationToken cancellationToken)
        => UnwrapIfPromiseCore(Timeout.InfiniteTimeSpan, cancellationToken);

    /// <summary>
    /// Returns a task for what a settled promise fulfils with, or for this value unchanged when it is not a
    /// promise.
    /// </summary>
    /// <param name="cancellationToken">The token to observe while waiting.</param>
    /// <returns>A task carrying the fulfilment value, or this value.</returns>
    /// <remarks>
    /// The calling thread is not blocked, and everything the operation itself does — a rejection included —
    /// is delivered through the returned task rather than thrown out of the call.
    /// </remarks>
    public Task<JsValue> UnwrapIfPromiseAsync(CancellationToken cancellationToken = default)
    {
        if (this is JsPromise promise)
        {
            return promise.Engine.UnwrapResultAsync(this, cancellationToken);
        }

        return Task.FromResult(this);
    }

    // A null timeout means "take the promise's own engine's configured Options.Constraints.PromiseTimeout";
    // a caller that named a bound gets exactly that bound, including Timeout.InfiniteTimeSpan. The engine is
    // only reachable once the value is known to be a promise, which is also the only case a bound applies to.
    private JsValue UnwrapIfPromiseCore(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (this is not JsPromise promise)
        {
            return this;
        }

        var effectiveTimeout = timeout ?? promise.Engine.Options.Constraints.PromiseTimeout;

        // Delegate to the engine's own drain rather than polling here. This used to be a
        // near-duplicate of it that predated EventLoop's work-arrived signal: it woke only on the
        // promise's own completion event, which a settle enqueued from a background thread never
        // sets, so every hop of an asynchronous chain idled out the full poll slice before this
        // thread ran the continuation that could advance it. DrainEventLoopUntilSettled waits on the
        // enqueue signal too - running that work on this thread is the only way the promise can
        // settle - and already carries the _waitingThreadId save/restore, its nesting, and the
        // engine's cancellation constraint.
        if (!promise.Engine.DrainEventLoopUntilSettled(promise, effectiveTimeout, cancellationToken))
        {
            Throw.PromiseRejectedException($"Timeout of {effectiveTimeout} reached");
        }

        switch (promise.State)
        {
            case PromiseState.Pending:
                Throw.InvalidOperationException("'UnwrapIfPromise' called before Promise was settled");
                return null;
            case PromiseState.Fulfilled:
                return promise.Value;
            case PromiseState.Rejected:
                Throw.PromiseRejectedException(promise.Value);
                return null;
            default:
                Throw.ArgumentOutOfRangeException();
                return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowWrongType(string expectedType)
    {
        Throw.ArgumentException($"Expected {expectedType} but got {_type}");
    }
}
