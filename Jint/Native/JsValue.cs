using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native;

/// <summary>
/// A JavaScript value: one of the primitives, or an object. Belongs to the engine that created it and must
/// not be shared with another.
/// </summary>
public abstract partial class JsValue : IEquatable<JsValue>
{
    public static readonly JsValue Undefined = new JsUndefined();
    public static readonly JsValue Null = new JsNull();

    // Not readonly: JsObject toggles InternalTypes.ShapeMode here when it enters/leaves hidden-class
    // shape mode, so the hot property paths can discriminate shape vs dictionary storage with a single
    // flag test on the already-loaded _type. Every other value type sets it once and never mutates it.
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal InternalTypes _type;

    protected JsValue(Types type)
    {
        _type = (InternalTypes) type;
    }

    internal JsValue(InternalTypes type)
    {
        _type = type;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-isarray
    /// The specification's IsArray, which is a different question from the public <see cref="IsArray"/>:
    /// it is true for every array exotic object (Array.prototype included) and follows a Proxy to its
    /// target, where the public one asks only whether this is a <see cref="JsArray"/>. Script reaches this
    /// one through Array.isArray; a host wants the other.
    /// </summary>
    [Pure]
    internal virtual bool IsSpecArray() => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsIntegerIndexedArray => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsConstructor => false;

    // Temporal type checks
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalDuration => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalInstant => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainDate => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainDateTime => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainMonthDay => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainTime => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainYearMonth => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalZonedDateTime => false;

    internal bool IsEmpty => ReferenceEquals(this, JsEmpty.Instance);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IteratorInstance GetIterator(Realm realm, GeneratorKind hint = GeneratorKind.Sync, ICallable? method = null)
    {
        if (!TryGetIterator(realm, out var iterator, hint, method))
        {
            Throw.TypeError(realm, "The value is not iterable");
            return null!;
        }

        return iterator;
    }

    [Pure]
    internal IteratorInstance GetIteratorFromMethod(Realm realm, ICallable method)
    {
        var iterator = method.Call(this);
        if (iterator is not ObjectInstance objectInstance)
        {
            Throw.TypeError(realm, "Result of the Symbol.iterator method is not an object");
            return null!;
        }
        return new IteratorInstance.ObjectIterator(objectInstance);
    }

    [Pure]
    internal virtual bool TryGetIterator(
        Realm realm,
        [NotNullWhen(true)] out IteratorInstance? iterator,
        GeneratorKind hint = GeneratorKind.Sync,
        ICallable? method = null)
    {
        // GetIterator (https://tc39.es/ecma262/#sec-getiterator) reads the method with GetMethod,
        // which is GetV: the lookup goes through ToObject, but the [[Get]] receiver -- and the this
        // value GetIteratorFromMethod (https://tc39.es/ecma262/#sec-getiteratorfrommethod) then
        // calls it with -- is the *original* value. A primitive therefore reaches a strict-mode
        // @@iterator as a primitive, not as the wrapper this lookup had to build.
        var obj = TypeConverter.ToObject(realm, this);

        if (method is null)
        {
            if (hint == GeneratorKind.Async)
            {
                method = GetMethod(realm, obj, this, GlobalSymbolRegistry.AsyncIterator);
                if (method is null)
                {
                    var syncMethod = GetMethod(realm, obj, this, GlobalSymbolRegistry.Iterator);
                    if (syncMethod is null)
                    {
                        iterator = null;
                        return false;
                    }
                    var syncIteratorRecord = GetIterator(realm, GeneratorKind.Sync, syncMethod);
                    // CreateAsyncFromSyncIterator - wrap the sync iterator in an async adapter
                    var asyncFromSync = new AsyncFromSyncIterator(obj.Engine, syncIteratorRecord);
                    iterator = new IteratorInstance.ObjectIterator(asyncFromSync);
                    return true;
                }
            }
            else
            {
                method = GetMethod(realm, obj, this, GlobalSymbolRegistry.Iterator);
            }
        }

        if (method is null)
        {
            iterator = null;
            return false;
        }

        var iteratorResult = method.Call(this, Arguments.Empty) as ObjectInstance;
        if (iteratorResult is null)
        {
            Throw.TypeError(realm, "Result of the Symbol.iterator method is not an object");
        }

        // GetIterator step 3 reads `next` off what @@iterator returned. A built-in iterator instance
        // is adopted as the record directly — its native stepping *is* that function's behaviour — but
        // only while the read would still resolve to it; otherwise the replacement has to drive the
        // iteration, which is what ObjectIterator does (it performs the read once, as the spec does).
        if (iteratorResult is IteratorInstance i && i.HasNativeNext)
        {
            iterator = i;
        }
        else
        {
            iterator = new IteratorInstance.ObjectIterator(iteratorResult);
        }

        return true;
    }

    /// <summary>
    /// Cached reflection lookups for Task interop to avoid repeated GetMethod/GetProperty calls.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> _taskResultPropertyCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> _valueTaskAsTaskMethodCache = new();

    internal static JsValue ConvertAwaitableToPromise(Engine engine, object obj)
    {
        if (obj is Task task)
        {
            return ConvertTaskToPromise(engine, task);
        }

#if !NETFRAMEWORK && !NETSTANDARD2_0
        if (obj is ValueTask valueTask)
        {
            return ConvertTaskToPromise(engine, valueTask.AsTask());
        }

        // ValueTask<T> - use cached reflection lookup
        var objType = obj.GetType();
        var asTask = _valueTaskAsTaskMethodCache.GetOrAdd(objType, static t => t.GetMethod(nameof(ValueTask<object>.AsTask)));
        if (asTask is not null)
        {
            return ConvertTaskToPromise(engine, (Task) asTask.Invoke(obj, parameters: null)!);
        }
#endif

        return FromObject(engine, JsValue.Undefined);
    }

    internal static JsValue ConvertTaskToPromise(Engine engine, Task task)
    {
        // The settle functions convert on the engine's thread, not on the background thread that completes
        // the Task. drainInline: false because this continuation runs ExecuteSynchronously and so can fire
        // on the engine's own thread mid-script; the waiting host or the next pump owns the drain.
        var (promise, resolveClr, rejectClr) = engine.RegisterPromise(drainInline: false);
        task = task.ContinueWith(continuationAction =>
            {
                if (continuationAction.IsFaulted)
                {
                    var aggregate = continuationAction.Exception;
                    if (FindConstraintFailure(aggregate) is { } failure)
                    {
                        var dispatch = ExceptionDispatchInfo.Capture(failure);
                        // A constraint failure ends its originating generation before this Task continuation
                        // observes it. Deliver the failure itself in the new generation; using the captured
                        // registration would fence off the only notification the awaiting host can receive.
                        engine.AddToEventLoop(dispatch.Throw, engine.EventLoopGeneration);
                    }
                    else
                    {
                        rejectClr(aggregate);
                    }
                }
                else if (continuationAction.IsCanceled)
                {
                    rejectClr(new ExecutionCanceledException());
                }
                else
                {
                    // Special case: Marshal `async Task` as undefined, as this is `Task<VoidTaskResult>` at runtime
                    // See https://github.com/sebastienros/jint/pull/1567#issuecomment-1681987702
                    if (Task.CompletedTask.Equals(continuationAction))
                    {
                        resolveClr(Undefined);
                        return;
                    }

                    // Use cached reflection lookup for Task<T>.Result property
                    var taskType = continuationAction.GetType();
                    var result = _taskResultPropertyCache.GetOrAdd(taskType, static t => t.GetProperty(nameof(Task<object>.Result)));
                    if (result is not null)
                    {
                        resolveClr(result.GetValue(continuationAction));
                    }
                    else
                    {
                        resolveClr(Undefined);
                    }
                }
            },
            // Ensure continuation is completed before unwrapping Promise
            continuationOptions: TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.ExecuteSynchronously);

        return promise;
    }

    private static Exception? FindConstraintFailure(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        // Throw.MustPropagateHostException rather than ConstraintFailure.MustPropagate: the latter adds
        // every TimeoutException and OperationCanceledException, which for a queued turn are Jint's own and
        // nothing else, but for a host Task are the two most ordinary failures there are — an HttpClient
        // timeout, a cancelled upload. Those must stay a promise rejection. Jint's own, raised through
        // Throw, are marked and match here anyway.
        if (Throw.MustPropagateHostException(exception))
        {
            return exception;
        }

        if (exception is TargetInvocationException { InnerException: { } inner })
        {
            return FindConstraintFailure(inner);
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var candidate in aggregate.InnerExceptions)
            {
                if (FindConstraintFailure(candidate) is { } failure)
                {
                    return failure;
                }
            }
        }

        return null;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public Types Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == InternalTypes.Integer
            ? Types.Number
            : (Types) (_type & ~InternalTypes.InternalFlags);
    }

    /// <summary>
    /// Creates a valid <see cref="JsValue"/> instance from any <see cref="Object"/> instance
    /// </summary>
    public static JsValue FromObject(Engine engine, object? value)
    {
        return FromObjectWithType(engine, value, null);
    }

    /// <summary>
    /// Creates a valid <see cref="JsValue"/> instance from any <see cref="Object"/> instance, with a type
    /// </summary>
    public static JsValue FromObjectWithType(Engine engine, object? value, Type? type)
    {
        using var ownership = engine.EnterHostCall();
        if (value is null)
        {
            return Null;
        }

        if (value is JsValue jsValue)
        {
            return jsValue;
        }

        if (engine._objectConverters != null)
        {
            foreach (var converter in engine._objectConverters)
            {
                if (converter.TryConvert(engine, value, out var result))
                {
                    return result;
                }
            }
        }

        if (DefaultObjectConverter.TryConvert(engine, value, type, out var defaultConversion))
        {
            return defaultConversion;
        }

        return null!;
    }

    /// <summary>
    /// Converts a <see cref="JsValue"/> to its underlying CLR value.
    /// </summary>
    /// <returns>The underlying CLR value of the <see cref="JsValue"/> instance.</returns>
    public abstract object? ToObject();

    /// <summary>
    /// Coerces boolean value from <see cref="JsValue"/> instance.
    /// </summary>
    internal virtual bool ToBoolean() => _type > InternalTypes.Null;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getv
    /// </summary>
    internal JsValue GetV(Realm realm, JsValue property)
    {
        // Number / Boolean / BigInt primitives resolve members on the intrinsic prototype without a
        // wrapper (see PrimitiveLookupPrototypeOrNull); everything else (notably String, whose wrapper
        // owns length and the indexed characters) still boxes via ToObject.
        var o = TypeConverter.PrimitiveLookupPrototypeOrNull(realm, this) ?? TypeConverter.ToObject(realm, this);
        return o.Get(property, this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue Get(JsValue property)
    {
        return Get(property, this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-o-p
    /// </summary>
    public virtual JsValue Get(JsValue property, JsValue receiver)
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-o-p-v-throw
    /// </summary>
    public virtual bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        Throw.NotSupportedException();
        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-instanceofoperator
    /// </summary>
    internal bool InstanceofOperator(JsValue target)
    {
        if (target is not ObjectInstance oi)
        {
            Throw.TypeErrorNoEngine("Right-hand side of 'instanceof' is not an object");
            return false;
        }

        var instOfHandler = oi.GetMethod(GlobalSymbolRegistry.HasInstance);
        if (instOfHandler is not null)
        {
            return TypeConverter.ToBoolean(instOfHandler.Call(target, this));
        }

        if (!target.HasCall)
        {
            Throw.TypeErrorNoEngine("Right-hand side of 'instanceof' is not callable");
        }

        return target.OrdinaryHasInstance(this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getmethod
    /// </summary>
    internal static ICallable? GetMethod(Realm realm, JsValue v, JsValue p)
    {
        // GetMethod uses GetV which converts primitives to objects
        // https://tc39.es/ecma262/#sec-getv
        var target = v is ObjectInstance obj ? obj : TypeConverter.ToObject(realm, v);
        return GetMethod(realm, target, v, p);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getmethod
    /// GetMethod for a caller that has already performed GetV's ToObject step. The
    /// <paramref name="receiver"/> is the original value, which is what [[Get]] takes as its
    /// receiver -- an accessor therefore sees the primitive rather than the wrapper the lookup
    /// had to build.
    /// </summary>
    internal static ICallable? GetMethod(Realm realm, ObjectInstance target, JsValue receiver, JsValue p)
    {
        var jsValue = target.Get(p, receiver);
        if (jsValue.IsNullOrUndefined())
        {
            return null;
        }

        var callable = jsValue as ICallable;
        if (callable is null)
        {
            Throw.TypeError(realm, $"Value returned for property '{p}' of object is not a function");
        }
        return callable;
    }

    public override string ToString()
    {
        return "None";
    }

    public static bool operator ==(JsValue? a, JsValue? b)
    {
        if (a is null)
        {
            return b is null;
        }

        return b is not null && a.Equals(b);
    }

    public static bool operator !=(JsValue? a, JsValue? b)
    {
        return !(a == b);
    }

    public static implicit operator JsValue(char value)
    {
        return JsString.Create(value);
    }

    public static implicit operator JsValue(int value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsValue(uint value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsValue(double value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsValue(long value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsValue(ulong value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsValue(BigInteger value)
    {
        return JsBigInt.Create(value);
    }

    public static implicit operator JsValue(bool value)
    {
        return value ? JsBoolean.True : JsBoolean.False;
    }

    [DebuggerStepThrough]
    public static implicit operator JsValue(string? value)
    {
        return value == null ? Null : JsString.Create(value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-islooselyequal
    /// </summary>
    protected internal virtual bool IsLooselyEqual(JsValue value)
    {
        if (ReferenceEquals(this, value))
        {
            return true;
        }

        // TODO move to type specific IsLooselyEqual

        var x = this;
        var y = value;

        if (x.IsNumber() && y.IsString())
        {
            return x.IsLooselyEqual(TypeConverter.ToNumber(y));
        }

        if (x.IsString() && y.IsNumber())
        {
            return y.IsLooselyEqual(TypeConverter.ToNumber(x));
        }

        if (x.IsBoolean())
        {
            return y.IsLooselyEqual(TypeConverter.ToNumber(x));
        }

        if (y.IsBoolean())
        {
            return x.IsLooselyEqual(TypeConverter.ToNumber(y));
        }

        if (y.IsObject() && (x._type & InternalTypes.Primitive) != InternalTypes.Empty)
        {
            return x.IsLooselyEqual(TypeConverter.ToPrimitive(y));
        }

        if (x.IsObject() && (y._type & InternalTypes.Primitive) != InternalTypes.Empty)
        {
            return y.IsLooselyEqual(TypeConverter.ToPrimitive(x));
        }

        return false;
    }

    /// <summary>
    /// Strict equality.
    /// </summary>
    public override bool Equals(object? obj) => Equals(obj as JsValue);

    /// <summary>
    /// Strict equality.
    /// </summary>
    public virtual bool Equals(JsValue? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => _type.GetHashCode();

    /// <summary>
    /// Some values need to be cloned in order to be assigned, like ConcatenatedString.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue Clone()
    {
        // concatenated string and arguments currently may require cloning
        return (_type & InternalTypes.RequiresCloning) == InternalTypes.Empty
            ? this
            : DoClone();
    }

    internal virtual JsValue DoClone() => this;

    /// <summary>
    /// Whether this value has a [[Call]] internal method, which is what the specification's IsCallable
    /// asks. The public spelling is <see cref="IsCallable"/>, a method, so this one cannot share the name.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool HasCall => this is ICallable;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinaryhasinstance
    /// </summary>
    internal virtual bool OrdinaryHasInstance(JsValue v)
    {
        if (!HasCall)
        {
            return false;
        }

        var o = v as ObjectInstance;
        if (o is null)
        {
            return false;
        }

        var p = Get(CommonProperties.Prototype);
        if (p is not ObjectInstance)
        {
            // p is proven not to be an object, so its own ToString() runs no script -- and unlike
            // TypeConverter.ToString it does not throw for a Symbol, which would replace this message.
            Throw.TypeError(o.Engine.Realm, $"Function has non-object prototype '{p}' in instanceof check");
        }

        while (true)
        {
            o = o.Prototype;

            if (o is null)
            {
                return false;
            }

            if (SameValue(p, o))
            {
                return true;
            }
        }
    }

    internal static bool SameValue(JsValue x, JsValue y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        var typea = x.Type;
        var typeb = y.Type;

        if (typea != typeb)
        {
            return false;
        }

        switch (typea)
        {
            case Types.Number:
                if (x._type == y._type && x._type == InternalTypes.Integer)
                {
                    return x.AsInteger() == y.AsInteger();
                }

                var nx = TypeConverter.ToNumber(x);
                var ny = TypeConverter.ToNumber(y);

                if (double.IsNaN(nx) && double.IsNaN(ny))
                {
                    return true;
                }

                if (nx == ny)
                {
                    if (nx == 0)
                    {
                        // +0 !== -0
                        return NumberInstance.IsNegativeZero(nx) == NumberInstance.IsNegativeZero(ny);
                    }

                    return true;
                }

                return false;
            case Types.String:
                return string.Equals(TypeConverter.ToString(x), TypeConverter.ToString(y), StringComparison.Ordinal);
            case Types.Boolean:
                return TypeConverter.ToBoolean(x) == TypeConverter.ToBoolean(y);
            case Types.Undefined:
            case Types.Null:
                return true;
            case Types.Symbol:
                return x == y;
            case Types.Object:
                return x is ObjectWrapper xo && y is ObjectWrapper yo && ReferenceEquals(xo.Target, yo.Target);
            case Types.BigInt:
                return (x is JsBigInt xBigInt && y is JsBigInt yBigInt && xBigInt.Equals(yBigInt));
            default:
                return false;
        }
    }

    internal static IConstructor AssertConstructor(Engine engine, JsValue c)
    {
        if (!c.IsConstructor)
        {
            Throw.TypeError(engine.Realm, $"{c.Type} is not a constructor");
        }

        return (IConstructor) c;
    }
}
