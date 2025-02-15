using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native;

public abstract partial class JsValue : IEquatable<JsValue>
{
    public static readonly JsValue Undefined = new JsUndefined();
    public static readonly JsValue Null = new JsNull();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal readonly InternalTypes _type;

    protected JsValue(Types type)
    {
        _type = (InternalTypes) type;
    }

    internal JsValue(InternalTypes type)
    {
        _type = type;
    }

    [Pure]
    internal virtual bool IsArray() => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsIntegerIndexedArray => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsConstructor => false;

    internal bool IsEmpty => ReferenceEquals(this, JsEmpty.Instance);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IteratorInstance GetIterator(Realm realm, GeneratorKind hint = GeneratorKind.Sync, ICallable? method = null)
    {
        if (!TryGetIterator(realm, out var iterator, hint, method))
        {
            ExceptionHelper.ThrowTypeError(realm, "The value is not iterable");
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
            ExceptionHelper.ThrowTypeError(realm);
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
        var obj = TypeConverter.ToObject(realm, this);

        if (method is null)
        {
            if (hint == GeneratorKind.Async)
            {
                method = obj.GetMethod(GlobalSymbolRegistry.AsyncIterator);
                if (method is null)
                {
                    var syncMethod = obj.GetMethod(GlobalSymbolRegistry.Iterator);
                    var syncIteratorRecord = obj.GetIterator(realm, GeneratorKind.Sync, syncMethod);
                    // TODO async CreateAsyncFromSyncIterator(syncIteratorRecord);
                    ExceptionHelper.ThrowNotImplementedException("async");
                }
            }
            else
            {
                method = obj.GetMethod(GlobalSymbolRegistry.Iterator);
            }
        }

        if (method is null)
        {
            iterator = null;
            return false;
        }

        var iteratorResult = method.Call(obj, Arguments.Empty) as ObjectInstance;
        if (iteratorResult is null)
        {
            ExceptionHelper.ThrowTypeError(realm, "Result of the Symbol.iterator method is not an object");
        }

        if (iteratorResult is IteratorInstance i)
        {
            iterator = i;
        }
        else
        {
            iterator = new IteratorInstance.ObjectIterator(iteratorResult);
        }

        return true;
    }

    internal static JsValue ConvertAwaitableToPromise(Engine engine, object obj)
    {
        if (obj is Task task)
        {
            return ConvertTaskToPromise(engine, task);
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            if (obj is ValueTask valueTask)
            {
                return ConvertTaskToPromise(engine, valueTask.AsTask());
            }

            // ValueTask<T>
            var asTask = obj.GetType().GetMethod(nameof(ValueTask<object>.AsTask));
            if (asTask is not null)
            {
                return ConvertTaskToPromise(engine, (Task) asTask.Invoke(obj, parameters: null)!);
            }
#endif

        return FromObject(engine, JsValue.Undefined);
    }

    internal static JsValue ConvertTaskToPromise(Engine engine, Task task)
    {
        var (promise, resolve, reject) = engine.RegisterPromise();
        task = task.ContinueWith(continuationAction =>
            {
                if (continuationAction.IsFaulted)
                {
                    reject(FromObject(engine, continuationAction.Exception));
                }
                else if (continuationAction.IsCanceled)
                {
                    reject(FromObject(engine, new ExecutionCanceledException()));
                }
                else
                {
                    // Special case: Marshal `async Task` as undefined, as this is `Task<VoidTaskResult>` at runtime
                    // See https://github.com/sebastienros/jint/pull/1567#issuecomment-1681987702
                    if (Task.CompletedTask.Equals(continuationAction))
                    {
                        resolve(FromObject(engine, JsValue.Undefined));
                        return;
                    }

                    var result = continuationAction.GetType().GetProperty(nameof(Task<object>.Result));
                    if (result is not null)
                    {
                        resolve(FromObject(engine, result.GetValue(continuationAction)));
                    }
                    else
                    {
                        resolve(FromObject(engine, JsValue.Undefined));
                    }
                }
            },
            // Ensure continuation is completed before unwrapping Promise
            continuationOptions: TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.ExecuteSynchronously);

        return promise;
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
        var o = TypeConverter.ToObject(realm, this);
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
        ExceptionHelper.ThrowNotSupportedException();
        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-instanceofoperator
    /// </summary>
    internal bool InstanceofOperator(JsValue target)
    {
        if (target is not ObjectInstance oi)
        {
            ExceptionHelper.ThrowTypeErrorNoEngine("Right-hand side of 'instanceof' is not an object");
            return false;
        }

        var instOfHandler = oi.GetMethod(GlobalSymbolRegistry.HasInstance);
        if (instOfHandler is not null)
        {
            return TypeConverter.ToBoolean(instOfHandler.Call(target, this));
        }

        if (!target.IsCallable)
        {
            ExceptionHelper.ThrowTypeErrorNoEngine("Right-hand side of 'instanceof' is not callable");
        }

        return target.OrdinaryHasInstance(this);
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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsCallable => this is ICallable;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinaryhasinstance
    /// </summary>
    internal virtual bool OrdinaryHasInstance(JsValue v)
    {
        if (!IsCallable)
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
            ExceptionHelper.ThrowTypeError(o.Engine.Realm, $"Function has non-object prototype '{TypeConverter.ToString(p)}' in instanceof check");
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
            default:
                return false;
        }
    }

    internal static IConstructor AssertConstructor(Engine engine, JsValue c)
    {
        if (!c.IsConstructor)
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, c + " is not a constructor");
        }

        return (IConstructor) c;
    }
}
