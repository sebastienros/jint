#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of methods return JsValue

using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Atomics;

/// <summary>
/// https://tc39.es/ecma262/#sec-atomics-object
/// </summary>
internal sealed class AtomicsInstance : ObjectInstance
{
    private readonly Realm _realm;

    /// <summary>
    /// Global waiters list for Atomics.wait/notify.
    /// Key is (buffer identity, byte index), value is list of waiting threads.
    /// </summary>
    private static readonly ConcurrentDictionary<WaiterKey, WaiterList> _waiters = new();

    private readonly struct WaiterKey : IEquatable<WaiterKey>
    {
        public readonly object Buffer;
        public readonly int ByteIndex;

        public WaiterKey(object buffer, int byteIndex)
        {
            Buffer = buffer;
            ByteIndex = byteIndex;
        }

        public bool Equals(WaiterKey other) => ReferenceEquals(Buffer, other.Buffer) && ByteIndex == other.ByteIndex;
        public override bool Equals(object? obj) => obj is WaiterKey other && Equals(other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Buffer) ^ ByteIndex;
    }

    private sealed class WaiterList
    {
        private readonly object _lock = new();
        private readonly List<Waiter> _syncWaiters = [];
        private readonly List<AsyncWaiter> _asyncWaiters = [];

        public int SyncCount
        {
            get
            {
                lock (_lock)
                {
                    return _syncWaiters.Count;
                }
            }
        }

        public int AddSync(Waiter waiter)
        {
            lock (_lock)
            {
                _syncWaiters.Add(waiter);
                return _syncWaiters.Count;
            }
        }

        public void RemoveSync(Waiter waiter)
        {
            lock (_lock)
            {
                _syncWaiters.Remove(waiter);
            }
        }

        public void AddAsync(AsyncWaiter waiter)
        {
            lock (_lock)
            {
                _asyncWaiters.Add(waiter);
            }
        }

        public void RemoveAsync(AsyncWaiter waiter)
        {
            lock (_lock)
            {
                _asyncWaiters.Remove(waiter);
            }
        }

        public int NotifyWaiters(int count)
        {
            var notified = 0;
            List<Waiter>? syncToRemove = null;
            List<AsyncWaiter>? asyncToNotify = null;

            lock (_lock)
            {
                // First notify sync waiters
                foreach (var waiter in _syncWaiters)
                {
                    if (notified >= count)
                    {
                        break;
                    }

                    lock (waiter.SyncRoot)
                    {
                        // Skip if already notified (handles race where waiter hasn't been removed yet)
                        if (waiter.Notified)
                        {
                            continue;
                        }

                        waiter.Notified = true;
                        Monitor.Pulse(waiter.SyncRoot);
                    }
                    notified++;

                    // Mark for removal from the list
                    syncToRemove ??= [];
                    syncToRemove.Add(waiter);
                }

                // Remove notified sync waiters from the list
                // This prevents double-counting in subsequent Notify calls
                if (syncToRemove != null)
                {
                    foreach (var waiter in syncToRemove)
                    {
                        _syncWaiters.Remove(waiter);
                    }
                }

                // Then notify async waiters
                foreach (var waiter in _asyncWaiters)
                {
                    if (notified >= count)
                    {
                        break;
                    }

                    if (!waiter.Resolved)
                    {
                        asyncToNotify ??= [];
                        asyncToNotify.Add(waiter);
                        notified++;
                    }
                }

                // Remove notified async waiters from the list
                if (asyncToNotify != null)
                {
                    foreach (var waiter in asyncToNotify)
                    {
                        _asyncWaiters.Remove(waiter);
                    }
                }
            }

            // Resolve async waiters outside the lock to avoid deadlocks
            if (asyncToNotify != null)
            {
                foreach (var waiter in asyncToNotify)
                {
                    waiter.Resolve("ok");
                }
            }

            return notified;
        }
    }

    private sealed class Waiter
    {
        public object SyncRoot { get; } = new();
        public bool Notified { get; set; }
    }

    private sealed class AsyncWaiter
    {
        private readonly PromiseCapability _promiseCapability;
        private readonly Engine _engine;
        private int _resolved;

        public AsyncWaiter(Engine engine, PromiseCapability promiseCapability)
        {
            _engine = engine;
            _promiseCapability = promiseCapability;
        }

        public bool Resolved => _resolved != 0;

        public void Resolve(string result)
        {
            if (Interlocked.CompareExchange(ref _resolved, 1, 0) == 0)
            {
                // Queue microtask to resolve the promise
                _engine.AddToEventLoop(() =>
                {
                    _promiseCapability.Resolve.Call(JsValue.Undefined, [new JsString(result)]);
                });
            }
        }
    }

    public AtomicsInstance(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(14, checkExistingKeys: false)
        {
            ["add"] = new(new ClrFunction(Engine, "add", Add, 3, LengthFlags), true, false, true),
            ["and"] = new(new ClrFunction(Engine, "and", And, 3, LengthFlags), true, false, true),
            ["compareExchange"] = new(new ClrFunction(Engine, "compareExchange", CompareExchange, 4, LengthFlags), true, false, true),
            ["exchange"] = new(new ClrFunction(Engine, "exchange", Exchange, 3, LengthFlags), true, false, true),
            ["isLockFree"] = new(new ClrFunction(Engine, "isLockFree", IsLockFree, 1, LengthFlags), true, false, true),
            ["load"] = new(new ClrFunction(Engine, "load", Load, 2, LengthFlags), true, false, true),
            ["notify"] = new(new ClrFunction(Engine, "notify", Notify, 3, LengthFlags), true, false, true),
            ["or"] = new(new ClrFunction(Engine, "or", Or, 3, LengthFlags), true, false, true),
            ["pause"] = new(new ClrFunction(Engine, "pause", Pause, 0, LengthFlags), true, false, true),
            ["store"] = new(new ClrFunction(Engine, "store", Store, 3, LengthFlags), true, false, true),
            ["sub"] = new(new ClrFunction(Engine, "sub", Sub, 3, LengthFlags), true, false, true),
            ["wait"] = new(new ClrFunction(Engine, "wait", Wait, 4, LengthFlags), true, false, true),
            ["waitAsync"] = new(new ClrFunction(Engine, "waitAsync", WaitAsync, 4, LengthFlags), true, false, true),
            ["xor"] = new(new ClrFunction(Engine, "xor", Xor, 3, LengthFlags), true, false, true),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("Atomics", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.add
    /// </summary>
    private JsValue Add(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);

        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Add);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.and
    /// </summary>
    private JsValue And(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);

        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.And);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.compareexchange
    /// </summary>
    private JsValue CompareExchange(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var expectedValue = arguments.At(2);
        var replacementValue = arguments.At(3);

        var taRecord = ValidateIntegerTypedArray(typedArray);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;

        TypedArrayValue expected, replacement;
        if (ta._contentType == TypedArrayContentType.BigInt)
        {
            expected = TypeConverter.ToBigInt(expectedValue);
            replacement = TypeConverter.ToBigInt(replacementValue);
        }
        else
        {
            expected = TypeConverter.ToIntegerOrInfinity(expectedValue);
            replacement = TypeConverter.ToIntegerOrInfinity(replacementValue);
        }

        ta._viewedArrayBuffer.AssertNotDetached();

        return DoAtomicCompareExchange(ta, byteIndexInBuffer, expected, replacement);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.exchange
    /// </summary>
    private JsValue Exchange(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);

        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Exchange);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.islockfree
    /// </summary>
    private static JsValue IsLockFree(JsValue thisObject, JsCallArguments arguments)
    {
        var size = arguments.At(0);
        var n = TypeConverter.ToIntegerOrInfinity(size);

        // Per spec: size 1, 2, 8 are implementation-defined, size 4 must return true
        // On modern hardware, all sizes 1, 2, 4, 8 are typically lock-free
        return n switch
        {
            1 => JsBoolean.True,  // Typically lock-free on modern systems
            2 => JsBoolean.True,  // Typically lock-free on modern systems
            4 => JsBoolean.True,  // Required by spec to be true
            8 => JsBoolean.True,  // Typically lock-free on 64-bit systems
            _ => JsBoolean.False
        };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.load
    /// </summary>
    private JsValue Load(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);

        var taRecord = ValidateIntegerTypedArray(typedArray);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;
        ta._viewedArrayBuffer.AssertNotDetached();

        return DoAtomicLoad(ta, byteIndexInBuffer);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.notify
    /// </summary>
    private JsValue Notify(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var count = arguments.At(2);

        var taRecord = ValidateIntegerTypedArray(typedArray, waitable: true);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;

        int c;
        if (count.IsUndefined())
        {
            c = int.MaxValue; // Infinity equivalent for waiter count
        }
        else
        {
            var intCount = TypeConverter.ToIntegerOrInfinity(count);
            c = (int) System.Math.Max(System.Math.Min(intCount, int.MaxValue), 0);
        }

        var buffer = ta._viewedArrayBuffer;

        // Per spec step 7: If IsSharedArrayBuffer(buffer) is false, return +0
        if (!buffer.IsSharedArrayBuffer)
        {
            return JsNumber.PositiveZero;
        }

        // Get the buffer's data array as the key - this is the shared memory
        var bufferData = buffer._arrayBufferData;
        if (bufferData is null)
        {
            return JsNumber.PositiveZero;
        }

        // Ensure we see the latest updates from other threads (important for ARM memory model)
        Thread.MemoryBarrier();

        var key = new WaiterKey(bufferData, byteIndexInBuffer);
        if (_waiters.TryGetValue(key, out var waiterList))
        {
            var notified = waiterList.NotifyWaiters(c);
            return JsNumber.Create(notified);
        }

        return JsNumber.PositiveZero;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.or
    /// </summary>
    private JsValue Or(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);

        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Or);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.pause
    /// </summary>
    private JsValue Pause(JsValue thisObject, JsCallArguments arguments)
    {
        var iterationNumber = arguments.At(0);
        if (!iterationNumber.IsUndefined())
        {
            if (!iterationNumber.IsNumber())
            {
                Throw.TypeError(_realm, "Invalid iteration count");
            }

            var n = TypeConverter.ToNumber(iterationNumber);
            if (!TypeConverter.IsIntegralNumber(n))
            {
                Throw.TypeError(_realm, "Invalid iteration count");
            }

            if (n < 0)
            {
                Throw.RangeError(_realm, "Invalid iteration count");
            }

            n = System.Math.Min(n, _engine.Options.Constraints.MaxAtomicsPauseIterations);
            Thread.SpinWait((int) n);
        }
        else
        {
            Thread.SpinWait(1);
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.store
    /// </summary>
    private JsValue Store(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);

        var taRecord = ValidateIntegerTypedArray(typedArray);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;

        TypedArrayValue v;
        if (ta._contentType == TypedArrayContentType.BigInt)
        {
            v = TypeConverter.ToBigInt(value);
        }
        else
        {
            v = TypeConverter.ToIntegerOrInfinity(value);
        }

        ta._viewedArrayBuffer.AssertNotDetached();

        DoAtomicStore(ta, byteIndexInBuffer, v);

        // Return the value that was stored (converted to appropriate type)
        if (ta._contentType == TypedArrayContentType.BigInt)
        {
            return JsBigInt.Create(v.BigInteger);
        }
        else
        {
            return JsNumber.Create(v.DoubleValue);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.sub
    /// </summary>
    private JsValue Sub(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);

        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Sub);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.wait
    /// </summary>
    private JsValue Wait(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);
        var timeout = arguments.At(3);

        var taRecord = ValidateIntegerTypedArray(typedArray, waitable: true, requireShared: true);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;

        TypedArrayValue v;
        if (ta._arrayElementType == TypedArrayElementType.BigInt64)
        {
            v = TypeConverter.ToBigInt64(TypeConverter.ToBigInt(value));
        }
        else
        {
            v = TypeConverter.ToInt32(value);
        }

        double q;
        if (timeout.IsUndefined())
        {
            q = double.PositiveInfinity;
        }
        else
        {
            q = TypeConverter.ToNumber(timeout);
            if (double.IsNaN(q))
            {
                q = double.PositiveInfinity;
            }
            else
            {
                q = System.Math.Max(q, 0);
            }
        }

        var buffer = ta._viewedArrayBuffer;
        var bufferData = buffer._arrayBufferData;
        if (bufferData is null)
        {
            return new JsString("not-equal");
        }

        // Check if value matches current value
        var currentValue = DoAtomicLoad(ta, byteIndexInBuffer);
        if (ta._arrayElementType == TypedArrayElementType.BigInt64)
        {
            var currentBigInt = ((JsBigInt) currentValue)._value;
            if (currentBigInt != v.BigInteger)
            {
                return new JsString("not-equal");
            }
        }
        else
        {
            var currentInt = (int) ((JsNumber) currentValue)._value;
            if (currentInt != (int) v.DoubleValue)
            {
                return new JsString("not-equal");
            }
        }

        // Value matches - add ourselves to the waiters list and block
        var key = new WaiterKey(bufferData, byteIndexInBuffer);
        var waiterList = _waiters.GetOrAdd(key, _ => new WaiterList());
        var waiter = new Waiter();
        waiterList.AddSync(waiter);

        // Ensure the waiter addition is visible to other threads (important for ARM memory model)
        Thread.MemoryBarrier();

        try
        {
            var timeoutMs = double.IsPositiveInfinity(q) ? -1 : (int) System.Math.Min(q, int.MaxValue);

            // Timeout of 0 means return immediately with "timed-out"
            if (timeoutMs == 0)
            {
                return new JsString("timed-out");
            }

            var stopwatch = timeoutMs > 0 ? System.Diagnostics.Stopwatch.StartNew() : null;

            lock (waiter.SyncRoot)
            {
                // Loop to handle spurious wakeups - keep waiting until notified or timeout truly elapsed
                while (!waiter.Notified)
                {
                    int remainingMs;
                    if (timeoutMs < 0)
                    {
                        remainingMs = -1; // Infinite wait
                    }
                    else
                    {
                        var elapsed = (int) stopwatch!.ElapsedMilliseconds;
                        remainingMs = timeoutMs - elapsed;
                        if (remainingMs <= 0)
                        {
                            break; // Timeout has truly elapsed
                        }
                    }

                    Monitor.Wait(waiter.SyncRoot, remainingMs);
                }
            }

            return waiter.Notified ? new JsString("ok") : new JsString("timed-out");
        }
        finally
        {
            waiterList.RemoveSync(waiter);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.waitasync
    /// </summary>
    private JsValue WaitAsync(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);
        var timeout = arguments.At(3);

        var taRecord = ValidateIntegerTypedArray(typedArray, waitable: true, requireShared: true);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;

        TypedArrayValue v;
        if (ta._arrayElementType == TypedArrayElementType.BigInt64)
        {
            v = TypeConverter.ToBigInt64(TypeConverter.ToBigInt(value));
        }
        else
        {
            v = TypeConverter.ToInt32(value);
        }

        double q;
        if (timeout.IsUndefined())
        {
            q = double.PositiveInfinity;
        }
        else
        {
            q = TypeConverter.ToNumber(timeout);
            if (double.IsNaN(q))
            {
                q = double.PositiveInfinity;
            }
            else
            {
                q = System.Math.Max(q, 0);
            }
        }

        var buffer = ta._viewedArrayBuffer;
        var bufferData = buffer._arrayBufferData;

        // Check if value matches current value
        var currentValue = DoAtomicLoad(ta, byteIndexInBuffer);
        bool valueMatches;
        if (ta._arrayElementType == TypedArrayElementType.BigInt64)
        {
            var currentBigInt = ((JsBigInt) currentValue)._value;
            valueMatches = currentBigInt == v.BigInteger;
        }
        else
        {
            var currentInt = (int) ((JsNumber) currentValue)._value;
            valueMatches = currentInt == (int) v.DoubleValue;
        }

        // If value doesn't match, return synchronous result
        if (!valueMatches)
        {
            var resultObj = OrdinaryObjectCreate(_engine, _realm.Intrinsics.Object.PrototypeObject);
            resultObj.Set(CommonProperties.Async, JsBoolean.False, throwOnError: true);
            resultObj.Set(CommonProperties.Value, new JsString("not-equal"), throwOnError: true);
            return resultObj;
        }

        // If timeout is 0 or less, return synchronous timed-out result
        if (q <= 0)
        {
            var resultObj = OrdinaryObjectCreate(_engine, _realm.Intrinsics.Object.PrototypeObject);
            resultObj.Set(CommonProperties.Async, JsBoolean.False, throwOnError: true);
            resultObj.Set(CommonProperties.Value, new JsString("timed-out"), throwOnError: true);
            return resultObj;
        }

        // Value matches and timeout > 0 - create a promise and add an async waiter
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        if (bufferData is not null)
        {
            var key = new WaiterKey(bufferData, byteIndexInBuffer);
            var waiterList = _waiters.GetOrAdd(key, _ => new WaiterList());
            var asyncWaiter = new AsyncWaiter(_engine, promiseCapability);
            waiterList.AddAsync(asyncWaiter);

            // Handle timeout
            if (!double.IsPositiveInfinity(q))
            {
                var timeoutMs = (int) System.Math.Min(q, int.MaxValue);
                var capturedWaiterList = waiterList;
                var capturedAsyncWaiter = asyncWaiter;

                // Use a timer to resolve with "timed-out" after the timeout
                _ = Task.Run(async () =>
                {
                    await Task.Delay(timeoutMs).ConfigureAwait(false);
                    if (!capturedAsyncWaiter.Resolved)
                    {
                        capturedWaiterList.RemoveAsync(capturedAsyncWaiter);
                        capturedAsyncWaiter.Resolve("timed-out");
                    }
                });
            }
        }
        else
        {
            // No buffer data - resolve immediately with "timed-out"
            _engine.AddToEventLoop(() =>
            {
                promiseCapability.Resolve.Call(JsValue.Undefined, [new JsString("timed-out")]);
            });
        }

        // Return an object with async: true and value: promise
        var asyncResultObj = OrdinaryObjectCreate(_engine, _realm.Intrinsics.Object.PrototypeObject);
        asyncResultObj.Set(CommonProperties.Async, JsBoolean.True, throwOnError: true);
        asyncResultObj.Set(CommonProperties.Value, promiseCapability.PromiseInstance, throwOnError: true);

        return asyncResultObj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.xor
    /// </summary>
    private JsValue Xor(JsValue thisObject, JsCallArguments arguments)
    {
        var typedArray = arguments.At(0);
        var index = arguments.At(1);
        var value = arguments.At(2);

        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Xor);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-validateintegertypedarray
    /// </summary>
    private IntrinsicTypedArrayPrototype.TypedArrayWithBufferWitnessRecord ValidateIntegerTypedArray(JsValue typedArray, bool waitable = false, bool requireShared = false)
    {
        var taRecord = typedArray.ValidateTypedArray(_realm, ArrayBufferOrder.Unordered);
        var ta = taRecord.Object;
        var type = ta._arrayElementType;

        if (waitable)
        {
            // Only Int32Array and BigInt64Array are waitable
            if (type != TypedArrayElementType.Int32 && type != TypedArrayElementType.BigInt64)
            {
                Throw.TypeError(_realm, "Atomics.wait/waitAsync/notify only works with Int32Array or BigInt64Array");
            }
        }
        else
        {
            // Must be an integer typed array (not float, not clamped)
            if (!IsIntegerTypedArray(type))
            {
                Throw.TypeError(_realm, "Typed array argument must be an integer typed array");
            }
        }

        // For wait/waitAsync, the buffer must be a SharedArrayBuffer
        // This check must happen before any argument coercion
        if (requireShared && !ta._viewedArrayBuffer.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Atomics.wait/waitAsync cannot be called on non-shared ArrayBuffer");
        }

        return taRecord;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-validateatomicaccess
    /// </summary>
    private int ValidateAtomicAccess(IntrinsicTypedArrayPrototype.TypedArrayWithBufferWitnessRecord taRecord, JsValue requestIndex)
    {
        // Per spec: length is retrieved from taRecord BEFORE index coercion
        var length = taRecord.TypedArrayLength;
        var accessIndex = TypeConverter.ToIndex(_realm, requestIndex);

        if (accessIndex >= length)
        {
            Throw.RangeError(_realm, "Invalid atomic access index");
        }

        var ta = taRecord.Object;
        var elementSize = ta._arrayElementType.GetElementSize();
        var offset = ta._byteOffset;
        return (int) (accessIndex * elementSize + offset);
    }

    private static bool IsIntegerTypedArray(TypedArrayElementType type)
    {
        return type switch
        {
            TypedArrayElementType.Int8 => true,
            TypedArrayElementType.Uint8 => true,
            TypedArrayElementType.Int16 => true,
            TypedArrayElementType.Uint16 => true,
            TypedArrayElementType.Int32 => true,
            TypedArrayElementType.Uint32 => true,
            TypedArrayElementType.BigInt64 => true,
            TypedArrayElementType.BigUint64 => true,
            _ => false // Float16, Float32, Float64, Uint8C are not integer types for Atomics
        };
    }

    private enum AtomicOperation
    {
        Add,
        Sub,
        And,
        Or,
        Xor,
        Exchange
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomicreadmodifywrite
    /// </summary>
    private JsValue AtomicReadModifyWrite(JsValue typedArrayValue, JsValue index, JsValue value, AtomicOperation op)
    {
        var taRecord = ValidateIntegerTypedArray(typedArrayValue);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;

        TypedArrayValue v;
        if (ta._contentType == TypedArrayContentType.BigInt)
        {
            v = TypeConverter.ToBigInt(value);
        }
        else
        {
            v = TypeConverter.ToIntegerOrInfinity(value);
        }

        ta._viewedArrayBuffer.AssertNotDetached();

        return DoAtomicOperation(ta, byteIndexInBuffer, v, op);
    }

    private static JsValue DoAtomicOperation(JsTypedArray ta, int byteIndex, TypedArrayValue value, AtomicOperation op)
    {
        var buffer = ta._viewedArrayBuffer._arrayBufferData!;
        var type = ta._arrayElementType;

        return type switch
        {
            TypedArrayElementType.Int8 => DoAtomicOperationInt8(buffer, byteIndex, DoubleToInt8(value.DoubleValue), op),
            TypedArrayElementType.Uint8 => DoAtomicOperationUint8(buffer, byteIndex, DoubleToUint8(value.DoubleValue), op),
            TypedArrayElementType.Int16 => DoAtomicOperationInt16(buffer, byteIndex, DoubleToInt16(value.DoubleValue), op),
            TypedArrayElementType.Uint16 => DoAtomicOperationUint16(buffer, byteIndex, DoubleToUint16(value.DoubleValue), op),
            TypedArrayElementType.Int32 => DoAtomicOperationInt32(buffer, byteIndex, DoubleToInt32(value.DoubleValue), op),
            TypedArrayElementType.Uint32 => DoAtomicOperationUint32(buffer, byteIndex, DoubleToUint32(value.DoubleValue), op),
            TypedArrayElementType.BigInt64 => DoAtomicOperationBigInt64(buffer, byteIndex, TypeConverter.ToBigInt64(value.BigInteger), op),
            TypedArrayElementType.BigUint64 => DoAtomicOperationBigUint64(buffer, byteIndex, TypeConverter.ToBigUint64(value.BigInteger), op),
            _ => throw new InvalidOperationException($"Unexpected typed array element type: {type}")
        };
    }

    // ECMAScript-compliant double-to-integer conversions
    private static int DoubleToInt32(double d)
    {
        if (d >= -(double) int.MinValue && d <= int.MaxValue)
        {
            return (int) d;
        }
        return TypeConverter.DoubleToInt32Slow(d);
    }

    private static uint DoubleToUint32(double d)
    {
        if (d is >= 0.0 and <= uint.MaxValue)
        {
            return (uint) d;
        }
        return (uint) TypeConverter.DoubleToInt32Slow(d);
    }

    private static short DoubleToInt16(double d)
    {
        return (short) DoubleToInt32(d);
    }

    private static ushort DoubleToUint16(double d)
    {
        return (ushort) DoubleToInt32(d);
    }

    private static sbyte DoubleToInt8(double d)
    {
        return (sbyte) DoubleToInt32(d);
    }

    private static byte DoubleToUint8(double d)
    {
        return (byte) DoubleToInt32(d);
    }

    private static JsValue DoAtomicLoad(JsTypedArray ta, int byteIndex)
    {
        var buffer = ta._viewedArrayBuffer._arrayBufferData!;
        var type = ta._arrayElementType;

        return type switch
        {
            TypedArrayElementType.Int8 => JsNumber.Create((sbyte) buffer[byteIndex]),
            TypedArrayElementType.Uint8 => JsNumber.Create(buffer[byteIndex]),
            TypedArrayElementType.Int16 => JsNumber.Create(ReadInt16(buffer, byteIndex)),
            TypedArrayElementType.Uint16 => JsNumber.Create(ReadUInt16(buffer, byteIndex)),
            TypedArrayElementType.Int32 => JsNumber.Create(ReadInt32(buffer, byteIndex)),
            TypedArrayElementType.Uint32 => JsNumber.Create(ReadUInt32(buffer, byteIndex)),
            TypedArrayElementType.BigInt64 => JsBigInt.Create(ReadInt64(buffer, byteIndex)),
            TypedArrayElementType.BigUint64 => JsBigInt.Create((BigInteger) ReadUInt64(buffer, byteIndex)),
            _ => throw new InvalidOperationException($"Unexpected typed array element type: {type}")
        };
    }

    private static void DoAtomicStore(JsTypedArray ta, int byteIndex, TypedArrayValue value)
    {
        var buffer = ta._viewedArrayBuffer._arrayBufferData!;
        var type = ta._arrayElementType;

        switch (type)
        {
            case TypedArrayElementType.Int8:
                buffer[byteIndex] = (byte) DoubleToInt8(value.DoubleValue);
                break;
            case TypedArrayElementType.Uint8:
                buffer[byteIndex] = DoubleToUint8(value.DoubleValue);
                break;
            case TypedArrayElementType.Int16:
                WriteInt16(buffer, byteIndex, DoubleToInt16(value.DoubleValue));
                break;
            case TypedArrayElementType.Uint16:
                WriteUInt16(buffer, byteIndex, DoubleToUint16(value.DoubleValue));
                break;
            case TypedArrayElementType.Int32:
                WriteInt32(buffer, byteIndex, DoubleToInt32(value.DoubleValue));
                break;
            case TypedArrayElementType.Uint32:
                WriteUInt32(buffer, byteIndex, DoubleToUint32(value.DoubleValue));
                break;
            case TypedArrayElementType.BigInt64:
                WriteInt64(buffer, byteIndex, TypeConverter.ToBigInt64(value.BigInteger));
                break;
            case TypedArrayElementType.BigUint64:
                WriteUInt64(buffer, byteIndex, TypeConverter.ToBigUint64(value.BigInteger));
                break;
        }
    }

    private static JsValue DoAtomicCompareExchange(JsTypedArray ta, int byteIndex, TypedArrayValue expected, TypedArrayValue replacement)
    {
        var buffer = ta._viewedArrayBuffer._arrayBufferData!;
        var type = ta._arrayElementType;

        return type switch
        {
            TypedArrayElementType.Int8 => DoCompareExchangeInt8(buffer, byteIndex, DoubleToInt8(expected.DoubleValue), DoubleToInt8(replacement.DoubleValue)),
            TypedArrayElementType.Uint8 => DoCompareExchangeUint8(buffer, byteIndex, DoubleToUint8(expected.DoubleValue), DoubleToUint8(replacement.DoubleValue)),
            TypedArrayElementType.Int16 => DoCompareExchangeInt16(buffer, byteIndex, DoubleToInt16(expected.DoubleValue), DoubleToInt16(replacement.DoubleValue)),
            TypedArrayElementType.Uint16 => DoCompareExchangeUint16(buffer, byteIndex, DoubleToUint16(expected.DoubleValue), DoubleToUint16(replacement.DoubleValue)),
            TypedArrayElementType.Int32 => DoCompareExchangeInt32(buffer, byteIndex, DoubleToInt32(expected.DoubleValue), DoubleToInt32(replacement.DoubleValue)),
            TypedArrayElementType.Uint32 => DoCompareExchangeUint32(buffer, byteIndex, DoubleToUint32(expected.DoubleValue), DoubleToUint32(replacement.DoubleValue)),
            TypedArrayElementType.BigInt64 => DoCompareExchangeBigInt64(buffer, byteIndex, TypeConverter.ToBigInt64(expected.BigInteger), TypeConverter.ToBigInt64(replacement.BigInteger)),
            TypedArrayElementType.BigUint64 => DoCompareExchangeBigUint64(buffer, byteIndex, TypeConverter.ToBigUint64(expected.BigInteger), TypeConverter.ToBigUint64(replacement.BigInteger)),
            _ => throw new InvalidOperationException($"Unexpected typed array element type: {type}")
        };
    }

    // Int8 operations (uses int32 with masking)
    private static JsValue DoAtomicOperationInt8(byte[] buffer, int byteIndex, sbyte value, AtomicOperation op)
    {
        var oldValue = (sbyte) buffer[byteIndex];
        sbyte newValue = op switch
        {
            AtomicOperation.Add => (sbyte) (oldValue + value),
            AtomicOperation.Sub => (sbyte) (oldValue - value),
            AtomicOperation.And => (sbyte) (oldValue & value),
            AtomicOperation.Or => (sbyte) (oldValue | value),
            AtomicOperation.Xor => (sbyte) (oldValue ^ value),
            AtomicOperation.Exchange => value,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
        buffer[byteIndex] = (byte) newValue;
        return JsNumber.Create(oldValue);
    }

    private static JsValue DoCompareExchangeInt8(byte[] buffer, int byteIndex, sbyte expected, sbyte replacement)
    {
        var oldValue = (sbyte) buffer[byteIndex];
        if (oldValue == expected)
        {
            buffer[byteIndex] = (byte) replacement;
        }
        return JsNumber.Create(oldValue);
    }

    // Uint8 operations
    private static JsValue DoAtomicOperationUint8(byte[] buffer, int byteIndex, byte value, AtomicOperation op)
    {
        var oldValue = buffer[byteIndex];
        byte newValue = op switch
        {
            AtomicOperation.Add => (byte) (oldValue + value),
            AtomicOperation.Sub => (byte) (oldValue - value),
            AtomicOperation.And => (byte) (oldValue & value),
            AtomicOperation.Or => (byte) (oldValue | value),
            AtomicOperation.Xor => (byte) (oldValue ^ value),
            AtomicOperation.Exchange => value,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
        buffer[byteIndex] = newValue;
        return JsNumber.Create(oldValue);
    }

    private static JsValue DoCompareExchangeUint8(byte[] buffer, int byteIndex, byte expected, byte replacement)
    {
        var oldValue = buffer[byteIndex];
        if (oldValue == expected)
        {
            buffer[byteIndex] = replacement;
        }
        return JsNumber.Create(oldValue);
    }

    // Int16 operations
    private static JsValue DoAtomicOperationInt16(byte[] buffer, int byteIndex, short value, AtomicOperation op)
    {
        var oldValue = ReadInt16(buffer, byteIndex);
        short newValue = op switch
        {
            AtomicOperation.Add => (short) (oldValue + value),
            AtomicOperation.Sub => (short) (oldValue - value),
            AtomicOperation.And => (short) (oldValue & value),
            AtomicOperation.Or => (short) (oldValue | value),
            AtomicOperation.Xor => (short) (oldValue ^ value),
            AtomicOperation.Exchange => value,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
        WriteInt16(buffer, byteIndex, newValue);
        return JsNumber.Create(oldValue);
    }

    private static JsValue DoCompareExchangeInt16(byte[] buffer, int byteIndex, short expected, short replacement)
    {
        var oldValue = ReadInt16(buffer, byteIndex);
        if (oldValue == expected)
        {
            WriteInt16(buffer, byteIndex, replacement);
        }
        return JsNumber.Create(oldValue);
    }

    // Uint16 operations
    private static JsValue DoAtomicOperationUint16(byte[] buffer, int byteIndex, ushort value, AtomicOperation op)
    {
        var oldValue = ReadUInt16(buffer, byteIndex);
        ushort newValue = op switch
        {
            AtomicOperation.Add => (ushort) (oldValue + value),
            AtomicOperation.Sub => (ushort) (oldValue - value),
            AtomicOperation.And => (ushort) (oldValue & value),
            AtomicOperation.Or => (ushort) (oldValue | value),
            AtomicOperation.Xor => (ushort) (oldValue ^ value),
            AtomicOperation.Exchange => value,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
        WriteUInt16(buffer, byteIndex, newValue);
        return JsNumber.Create(oldValue);
    }

    private static JsValue DoCompareExchangeUint16(byte[] buffer, int byteIndex, ushort expected, ushort replacement)
    {
        var oldValue = ReadUInt16(buffer, byteIndex);
        if (oldValue == expected)
        {
            WriteUInt16(buffer, byteIndex, replacement);
        }
        return JsNumber.Create(oldValue);
    }

    // Int32 operations - use Interlocked for thread safety
    private static unsafe JsValue DoAtomicOperationInt32(byte[] buffer, int byteIndex, int value, AtomicOperation op)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref int location = ref Unsafe.AsRef<int>(ptr);
            int oldValue;

            switch (op)
            {
                case AtomicOperation.Add:
                    oldValue = Interlocked.Add(ref location, value) - value;
                    break;
                case AtomicOperation.Sub:
                    oldValue = Interlocked.Add(ref location, -value) + value;
                    break;
                case AtomicOperation.Exchange:
                    oldValue = Interlocked.Exchange(ref location, value);
                    break;
                case AtomicOperation.And:
                    oldValue = InterlockedAnd(ref location, value);
                    break;
                case AtomicOperation.Or:
                    oldValue = InterlockedOr(ref location, value);
                    break;
                case AtomicOperation.Xor:
                    oldValue = InterlockedXor(ref location, value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }

            return JsNumber.Create(oldValue);
        }
    }

    private static unsafe JsValue DoCompareExchangeInt32(byte[] buffer, int byteIndex, int expected, int replacement)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref int location = ref Unsafe.AsRef<int>(ptr);
            var oldValue = Interlocked.CompareExchange(ref location, replacement, expected);
            return JsNumber.Create(oldValue);
        }
    }

    // Uint32 operations
    private static unsafe JsValue DoAtomicOperationUint32(byte[] buffer, int byteIndex, uint value, AtomicOperation op)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref int location = ref Unsafe.AsRef<int>(ptr);
            int oldValue;

            switch (op)
            {
                case AtomicOperation.Add:
                    oldValue = Interlocked.Add(ref location, (int) value) - (int) value;
                    break;
                case AtomicOperation.Sub:
                    oldValue = Interlocked.Add(ref location, -(int) value) + (int) value;
                    break;
                case AtomicOperation.Exchange:
                    oldValue = Interlocked.Exchange(ref location, (int) value);
                    break;
                case AtomicOperation.And:
                    oldValue = InterlockedAnd(ref location, (int) value);
                    break;
                case AtomicOperation.Or:
                    oldValue = InterlockedOr(ref location, (int) value);
                    break;
                case AtomicOperation.Xor:
                    oldValue = InterlockedXor(ref location, (int) value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }

            return JsNumber.Create((uint) oldValue);
        }
    }

    private static unsafe JsValue DoCompareExchangeUint32(byte[] buffer, int byteIndex, uint expected, uint replacement)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref int location = ref Unsafe.AsRef<int>(ptr);
            var oldValue = Interlocked.CompareExchange(ref location, (int) replacement, (int) expected);
            return JsNumber.Create((uint) oldValue);
        }
    }

    // BigInt64 operations
    private static unsafe JsValue DoAtomicOperationBigInt64(byte[] buffer, int byteIndex, long value, AtomicOperation op)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref long location = ref Unsafe.AsRef<long>(ptr);
            long oldValue;

            switch (op)
            {
                case AtomicOperation.Add:
                    oldValue = Interlocked.Add(ref location, value) - value;
                    break;
                case AtomicOperation.Sub:
                    oldValue = Interlocked.Add(ref location, -value) + value;
                    break;
                case AtomicOperation.Exchange:
                    oldValue = Interlocked.Exchange(ref location, value);
                    break;
                case AtomicOperation.And:
                    oldValue = InterlockedAnd64(ref location, value);
                    break;
                case AtomicOperation.Or:
                    oldValue = InterlockedOr64(ref location, value);
                    break;
                case AtomicOperation.Xor:
                    oldValue = InterlockedXor64(ref location, value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }

            return JsBigInt.Create(oldValue);
        }
    }

    private static unsafe JsValue DoCompareExchangeBigInt64(byte[] buffer, int byteIndex, long expected, long replacement)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref long location = ref Unsafe.AsRef<long>(ptr);
            var oldValue = Interlocked.CompareExchange(ref location, replacement, expected);
            return JsBigInt.Create(oldValue);
        }
    }

    // BigUint64 operations
    private static unsafe JsValue DoAtomicOperationBigUint64(byte[] buffer, int byteIndex, ulong value, AtomicOperation op)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref long location = ref Unsafe.AsRef<long>(ptr);
            long oldValue;

            switch (op)
            {
                case AtomicOperation.Add:
                    oldValue = Interlocked.Add(ref location, (long) value) - (long) value;
                    break;
                case AtomicOperation.Sub:
                    oldValue = Interlocked.Add(ref location, -(long) value) + (long) value;
                    break;
                case AtomicOperation.Exchange:
                    oldValue = Interlocked.Exchange(ref location, (long) value);
                    break;
                case AtomicOperation.And:
                    oldValue = InterlockedAnd64(ref location, (long) value);
                    break;
                case AtomicOperation.Or:
                    oldValue = InterlockedOr64(ref location, (long) value);
                    break;
                case AtomicOperation.Xor:
                    oldValue = InterlockedXor64(ref location, (long) value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }

            return JsBigInt.Create((BigInteger) (ulong) oldValue);
        }
    }

    private static unsafe JsValue DoCompareExchangeBigUint64(byte[] buffer, int byteIndex, ulong expected, ulong replacement)
    {
        fixed (byte* ptr = &buffer[byteIndex])
        {
            ref long location = ref Unsafe.AsRef<long>(ptr);
            var oldValue = Interlocked.CompareExchange(ref location, (long) replacement, (long) expected);
            return JsBigInt.Create((BigInteger) (ulong) oldValue);
        }
    }

    // Helper methods for reading/writing multi-byte values
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ReadInt16(byte[] buffer, int byteIndex)
    {
        return (short) (buffer[byteIndex] | (buffer[byteIndex + 1] << 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt16(byte[] buffer, int byteIndex, short value)
    {
        buffer[byteIndex] = (byte) value;
        buffer[byteIndex + 1] = (byte) (value >> 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadUInt16(byte[] buffer, int byteIndex)
    {
        return (ushort) (buffer[byteIndex] | (buffer[byteIndex + 1] << 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt16(byte[] buffer, int byteIndex, ushort value)
    {
        buffer[byteIndex] = (byte) value;
        buffer[byteIndex + 1] = (byte) (value >> 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(byte[] buffer, int byteIndex)
    {
        return buffer[byteIndex] | (buffer[byteIndex + 1] << 8) | (buffer[byteIndex + 2] << 16) | (buffer[byteIndex + 3] << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32(byte[] buffer, int byteIndex, int value)
    {
        buffer[byteIndex] = (byte) value;
        buffer[byteIndex + 1] = (byte) (value >> 8);
        buffer[byteIndex + 2] = (byte) (value >> 16);
        buffer[byteIndex + 3] = (byte) (value >> 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32(byte[] buffer, int byteIndex)
    {
        return (uint) (buffer[byteIndex] | (buffer[byteIndex + 1] << 8) | (buffer[byteIndex + 2] << 16) | (buffer[byteIndex + 3] << 24));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt32(byte[] buffer, int byteIndex, uint value)
    {
        buffer[byteIndex] = (byte) value;
        buffer[byteIndex + 1] = (byte) (value >> 8);
        buffer[byteIndex + 2] = (byte) (value >> 16);
        buffer[byteIndex + 3] = (byte) (value >> 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadInt64(byte[] buffer, int byteIndex)
    {
        var lo = (uint) (buffer[byteIndex] | (buffer[byteIndex + 1] << 8) | (buffer[byteIndex + 2] << 16) | (buffer[byteIndex + 3] << 24));
        var hi = (uint) (buffer[byteIndex + 4] | (buffer[byteIndex + 5] << 8) | (buffer[byteIndex + 6] << 16) | (buffer[byteIndex + 7] << 24));
        return (long) ((ulong) hi << 32 | lo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt64(byte[] buffer, int byteIndex, long value)
    {
        buffer[byteIndex] = (byte) value;
        buffer[byteIndex + 1] = (byte) (value >> 8);
        buffer[byteIndex + 2] = (byte) (value >> 16);
        buffer[byteIndex + 3] = (byte) (value >> 24);
        buffer[byteIndex + 4] = (byte) (value >> 32);
        buffer[byteIndex + 5] = (byte) (value >> 40);
        buffer[byteIndex + 6] = (byte) (value >> 48);
        buffer[byteIndex + 7] = (byte) (value >> 56);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64(byte[] buffer, int byteIndex)
    {
        var lo = (uint) (buffer[byteIndex] | (buffer[byteIndex + 1] << 8) | (buffer[byteIndex + 2] << 16) | (buffer[byteIndex + 3] << 24));
        var hi = (uint) (buffer[byteIndex + 4] | (buffer[byteIndex + 5] << 8) | (buffer[byteIndex + 6] << 16) | (buffer[byteIndex + 7] << 24));
        return (ulong) hi << 32 | lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt64(byte[] buffer, int byteIndex, ulong value)
    {
        buffer[byteIndex] = (byte) value;
        buffer[byteIndex + 1] = (byte) (value >> 8);
        buffer[byteIndex + 2] = (byte) (value >> 16);
        buffer[byteIndex + 3] = (byte) (value >> 24);
        buffer[byteIndex + 4] = (byte) (value >> 32);
        buffer[byteIndex + 5] = (byte) (value >> 40);
        buffer[byteIndex + 6] = (byte) (value >> 48);
        buffer[byteIndex + 7] = (byte) (value >> 56);
    }

    // Interlocked.And/Or/Xor are only available in .NET 5.0+
    // For older frameworks, we use CompareExchange loops
#if NET5_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InterlockedAnd(ref int location, int value)
    {
        return Interlocked.And(ref location, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InterlockedOr(ref int location, int value)
    {
        return Interlocked.Or(ref location, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long InterlockedAnd64(ref long location, long value)
    {
        return Interlocked.And(ref location, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long InterlockedOr64(ref long location, long value)
    {
        return Interlocked.Or(ref location, value);
    }
#else
    private static int InterlockedAnd(ref int location, int value)
    {
        int current = location;
        while (true)
        {
            int newValue = current & value;
            int oldValue = Interlocked.CompareExchange(ref location, newValue, current);
            if (oldValue == current)
            {
                return current;
            }
            current = oldValue;
        }
    }

    private static int InterlockedOr(ref int location, int value)
    {
        int current = location;
        while (true)
        {
            int newValue = current | value;
            int oldValue = Interlocked.CompareExchange(ref location, newValue, current);
            if (oldValue == current)
            {
                return current;
            }
            current = oldValue;
        }
    }

    private static long InterlockedAnd64(ref long location, long value)
    {
        long current = Interlocked.Read(ref location);
        while (true)
        {
            long newValue = current & value;
            long oldValue = Interlocked.CompareExchange(ref location, newValue, current);
            if (oldValue == current)
            {
                return current;
            }
            current = oldValue;
        }
    }

    private static long InterlockedOr64(ref long location, long value)
    {
        long current = Interlocked.Read(ref location);
        while (true)
        {
            long newValue = current | value;
            long oldValue = Interlocked.CompareExchange(ref location, newValue, current);
            if (oldValue == current)
            {
                return current;
            }
            current = oldValue;
        }
    }
#endif

    // XOR is not available in any .NET version via Interlocked, so always use CompareExchange loop
    private static int InterlockedXor(ref int location, int value)
    {
        int current = location;
        while (true)
        {
            int newValue = current ^ value;
            int oldValue = Interlocked.CompareExchange(ref location, newValue, current);
            if (oldValue == current)
            {
                return current;
            }
            current = oldValue;
        }
    }

    private static long InterlockedXor64(ref long location, long value)
    {
        long current = Interlocked.Read(ref location);
        while (true)
        {
            long newValue = current ^ value;
            long oldValue = Interlocked.CompareExchange(ref location, newValue, current);
            if (oldValue == current)
            {
                return current;
            }
            current = oldValue;
        }
    }
}
