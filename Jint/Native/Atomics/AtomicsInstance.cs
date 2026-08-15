#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of methods return JsValue

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Atomics;

/// <summary>
/// https://tc39.es/ecma262/#sec-atomics-object
/// </summary>
[JsObject]
internal sealed partial class AtomicsInstance : BuiltinShapeObject
{
    private readonly Realm _realm;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString AtomicsToStringTag = new("Atomics");

    /// <summary>
    /// The waiter lists of every shared data block this process has waited on, one entry per block, each
    /// holding one list per byte index — the spec's store of WaiterList Records, "indexed by (block, i)" and
    /// "agent-independent" (https://tc39.es/ecma262/#sec-waiterlist-records).
    /// </summary>
    /// <remarks>
    /// The table is keyed <b>weakly</b> on the block, and that is what bounds a waiter's lifetime. An async
    /// waiter holds its engine — that is how it resolves its promise back onto the right event loop — and a
    /// wait asking for no timeout never resolves and is never removed, so keying this strongly kept the engine,
    /// its realm and everything they root alive for the life of the process. Weakly it lives exactly as long as
    /// the block, which is the lifetime the spec gives it: while any agent can still reach the block it can
    /// still call <c>Atomics.notify</c>, so the entry has to survive to be woken; once no agent can, nothing
    /// can ever notify it again and the whole cycle — block, engine, waiter — is collected together. The engine
    /// reference stays strong on purpose, so what <c>Atomics.notify</c> counts never depends on when a
    /// collection happened to run.
    /// <para>
    /// A block whose lists have all been pruned keeps an empty entry here until the block itself dies. Removing
    /// it would race a thread that already holds the <see cref="WaiterBlock"/> and is about to add to it, and
    /// what it would save is one empty object per block that is about to be collected anyway.
    /// </para>
    /// </remarks>
    private static readonly ConditionalWeakTable<byte[], WaiterBlock> _blocks = new();

    private static readonly ConditionalWeakTable<byte[], WaiterBlock>.CreateValueCallback _createWaiterBlock = static _ => new WaiterBlock();

    /// <summary>
    /// How many per-index waiter lists the registry currently holds for a shared data block. Nothing in the
    /// engine reads this; it exists so the pruning invariant — a list that has emptied is removed — can be
    /// asserted from a test.
    /// </summary>
    internal static int WaiterListCount(byte[] block) => _blocks.TryGetValue(block, out var waiters) ? waiters.Count : 0;

    /// <summary>
    /// The waiter lists of one shared data block, indexed by byte index.
    /// </summary>
    /// <remarks>
    /// One lock guards both this dictionary and the contents of every list in it, which is what makes creating
    /// a list and adding the waiter that needed it a single step. Two steps could not be made safe: a list
    /// obtained from the dictionary and added to afterwards can be pruned in between, and the waiter then joins
    /// a list nothing can find. The order is block lock → <see cref="Waiter.SyncRoot"/>, never the reverse: a
    /// suspended agent releases its own monitor before it asks for this one.
    /// </remarks>
    private sealed class WaiterBlock
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<int, WaiterList> _lists = new();

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _lists.Count;
                }
            }
        }

        public WaiterList AddSync(int byteIndex, Waiter waiter)
        {
            lock (_lock)
            {
                var list = GetOrCreate(byteIndex);
                list.SyncWaiters.Add(waiter);
                return list;
            }
        }

        public WaiterList AddAsync(int byteIndex, AsyncWaiter waiter)
        {
            lock (_lock)
            {
                var list = GetOrCreate(byteIndex);
                list.AsyncWaiters.Add(waiter);
                return list;
            }
        }

        public void RemoveSync(WaiterList list, Waiter waiter)
        {
            lock (_lock)
            {
                list.SyncWaiters.Remove(waiter);
                PruneIfEmpty(list);
            }
        }

        public void RemoveAsync(WaiterList list, AsyncWaiter waiter)
        {
            lock (_lock)
            {
                list.AsyncWaiters.Remove(waiter);
                PruneIfEmpty(list);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-notifywaiter
        /// </summary>
        public int Notify(int byteIndex, int count)
        {
            int notified;
            List<AsyncWaiter>? asyncToNotify;

            lock (_lock)
            {
                if (!_lists.TryGetValue(byteIndex, out var list))
                {
                    return 0;
                }

                notified = NotifyWaiters(list, count, out asyncToNotify);
                PruneIfEmpty(list);
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

        private static int NotifyWaiters(WaiterList list, int count, out List<AsyncWaiter>? asyncToNotify)
        {
            var notified = 0;
            List<Waiter>? syncToRemove = null;
            asyncToNotify = null;

            // First notify sync waiters
            foreach (var waiter in list.SyncWaiters)
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
                    list.SyncWaiters.Remove(waiter);
                }
            }

            // Then notify async waiters
            foreach (var waiter in list.AsyncWaiters)
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
                    list.AsyncWaiters.Remove(waiter);
                }
            }

            return notified;
        }

        private WaiterList GetOrCreate(int byteIndex)
        {
            if (!_lists.TryGetValue(byteIndex, out var list))
            {
                list = new WaiterList(byteIndex);
                _lists[byteIndex] = list;
            }

            return list;
        }

        /// <summary>
        /// Drops a list nobody is waiting in any more. Every wait that ever ended used to leave its list here
        /// forever, one per index the script touched. The caller holds the lock, so no waiter can be joining
        /// the list being examined, and the identity check keeps a stale reference — a list already pruned and
        /// replaced by a later wait on the same index — from removing its successor.
        /// </summary>
        private void PruneIfEmpty(WaiterList list)
        {
            if (list.SyncWaiters.Count != 0 || list.AsyncWaiters.Count != 0)
            {
                return;
            }

            if (_lists.TryGetValue(list.ByteIndex, out var current) && ReferenceEquals(current, list))
            {
                _lists.Remove(list.ByteIndex);
            }
        }
    }

    /// <summary>
    /// The waiters of one byte index. Both lists are guarded by the owning <see cref="WaiterBlock"/>'s lock.
    /// </summary>
    private sealed class WaiterList(int byteIndex)
    {
        public int ByteIndex { get; } = byteIndex;
        public List<Waiter> SyncWaiters { get; } = [];
        public List<AsyncWaiter> AsyncWaiters { get; } = [];
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

        /// <summary>
        /// The evaluation cycle the wait was registered in, read here on the engine thread. Neither of the
        /// two ways a wait ends runs on that thread: the timeout fires on a timer thread, and a wake arrives
        /// from whichever agent calls <c>Atomics.notify</c> on the shared buffer. Reading the generation at
        /// settle time would therefore read whatever cycle the engine is in by then, and a wait registered
        /// before a <c>RestoreGlobalSnapshot</c> would resolve its promise into the restored engine.
        /// </summary>
        private readonly int _generation;

        /// <summary>
        /// Cancelled by whichever route settles this wait first, which is what stops the timeout timer.
        /// Only a wait with a finite timeout has one — an infinite wait starts no timer, so there is
        /// nothing to cancel.
        /// </summary>
        /// <remarks>
        /// Deliberately never disposed. Nothing here reaches the two things a <c>CancellationTokenSource</c>
        /// needs disposing for — <c>CancelAfter</c>'s timer and <c>Token.WaitHandle</c>'s kernel event — so
        /// the source is plain garbage once the wait has settled, and both ends drop it together. Disposing
        /// it would have to happen on the timer task, which is the one thread that cannot know whether the
        /// <c>Cancel</c> that woke it has finished running its registrations.
        /// </remarks>
        private readonly CancellationTokenSource? _timeoutCancellation;

        private int _resolved;

        public AsyncWaiter(Engine engine, PromiseCapability promiseCapability, bool hasTimeout)
        {
            _engine = engine;
            _promiseCapability = promiseCapability;
            _generation = engine.EventLoopGeneration;
            _timeoutCancellation = hasTimeout ? new CancellationTokenSource() : null;
        }

        public bool Resolved => _resolved != 0;

        public CancellationToken TimeoutToken => _timeoutCancellation?.Token ?? CancellationToken.None;

        public void Resolve(string result)
        {
            if (Interlocked.CompareExchange(ref _resolved, 1, 0) == 0)
            {
                // Stop the timeout timer before anything else. A wake that arrives first would otherwise
                // leave a thread-pool task sleeping out the rest of the interval with this waiter — and
                // through it the engine, its realm and the promise capability — alive in its closure, long
                // after the engine had finished with the wait. Cancelling here is what bounds the timer by
                // the wait rather than by the clock, whichever of the two routes wins.
                _timeoutCancellation?.Cancel();

                // Queue microtask to resolve the promise
                _engine.AddToEventLoop(() =>
                {
                    _promiseCapability.Resolve(new JsString(result));
                }, _generation);
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
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.add
    /// </summary>
    [JsFunction]
    private JsValue Add(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value)
    {
        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Add);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.and
    /// </summary>
    [JsFunction]
    private JsValue And(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value)
    {
        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.And);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.compareexchange
    /// </summary>
    [JsFunction]
    private JsValue CompareExchange(JsValue thisObject, JsValue typedArray, JsValue index, JsValue expectedValue, JsValue replacementValue)
    {
        var taRecord = ValidateIntegerTypedArray(typedArray, isWrite: true);
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
    [JsFunction]
    private JsValue Exchange(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value)
    {
        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Exchange);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.islockfree
    /// </summary>
    [JsFunction]
    private static JsValue IsLockFree(JsValue thisObject, JsValue size)
    {
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
    [JsFunction]
    private JsValue Load(JsValue thisObject, JsValue typedArray, JsValue index)
    {
        var taRecord = ValidateIntegerTypedArray(typedArray);
        var byteIndexInBuffer = ValidateAtomicAccess(taRecord, index);
        var ta = taRecord.Object;
        ta._viewedArrayBuffer.AssertNotDetached();

        return DoAtomicLoad(ta, byteIndexInBuffer);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.notify
    /// </summary>
    [JsFunction]
    private JsValue Notify(JsValue thisObject, JsValue typedArray, JsValue index, JsValue count)
    {
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

        if (_blocks.TryGetValue(bufferData, out var waiters))
        {
            return JsNumber.Create(waiters.Notify(byteIndexInBuffer, c));
        }

        return JsNumber.PositiveZero;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.or
    /// </summary>
    [JsFunction]
    private JsValue Or(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value)
    {
        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Or);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.pause
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue Pause(JsValue thisObject, JsValue iterationNumber)
    {
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
    [JsFunction]
    private JsValue Store(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value)
    {
        var taRecord = ValidateIntegerTypedArray(typedArray, isWrite: true);
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
    [JsFunction]
    private JsValue Sub(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value)
    {
        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Sub);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.wait
    /// </summary>
    [JsFunction]
    private JsValue Wait(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value, JsValue timeout)
    {
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

        // https://tc39.es/ecma262/#sec-dowait step 8
        if (!Engine.Options.AgentCanSuspend)
        {
            Throw.TypeError(_realm, "Atomics.wait cannot be used in this agent");
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
        var waiters = _blocks.GetValue(bufferData, _createWaiterBlock);
        var waiter = new Waiter();
        var waiterList = waiters.AddSync(byteIndexInBuffer, waiter);

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
            waiters.RemoveSync(waiterList, waiter);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-atomics.waitasync
    /// </summary>
    [JsFunction]
    private JsValue WaitAsync(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value, JsValue timeout)
    {
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
            var waiters = _blocks.GetValue(bufferData, _createWaiterBlock);
            var hasTimeout = !double.IsPositiveInfinity(q);
            var asyncWaiter = new AsyncWaiter(_engine, promiseCapability, hasTimeout);
            var waiterList = waiters.AddAsync(byteIndexInBuffer, asyncWaiter);

            // Handle timeout
            if (hasTimeout)
            {
                var timeoutMs = (int) System.Math.Min(System.Math.Ceiling(q), int.MaxValue);
                var capturedWaiters = waiters;
                var capturedWaiterList = waiterList;
                var capturedAsyncWaiter = asyncWaiter;
                var timeoutToken = asyncWaiter.TimeoutToken;

                // Use a timer to resolve with "timed-out" after the timeout. Task.Delay can
                // complete slightly ahead of the requested interval (timer granularity /
                // coalescing), and the wait must not report timed-out before the timeout has
                // actually elapsed — script can observe the lapse (test262 asserts
                // lapse >= timeout) — so re-delay until a monotonic clock agrees.
                //
                // The delay observes the waiter's own cancellation, which AsyncWaiter.Resolve
                // triggers however the wait ends. Without it the task was unstoppable: an
                // Atomics.notify that woke the wait after a millisecond still left a thread-pool
                // task sleeping out the whole interval, holding the waiter — and through it the
                // engine, its realm and the promise capability — alive in its closure, and then
                // enqueueing onto an event loop whose engine the host had long finished with.
                // The window is as long as the script asked for, so nothing bounds it but the
                // script, and a host that builds an engine per request accumulated one such task
                // per unfinished wait.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var remainingMs = timeoutMs;
                        do
                        {
                            await Task.Delay(remainingMs, timeoutToken).ConfigureAwait(false);
                            remainingMs = timeoutMs - (int) stopwatch.ElapsedMilliseconds;
                        } while (remainingMs > 0);
                    }
                    catch (OperationCanceledException)
                    {
                        // The wait was settled by another route; it has already been removed from
                        // the list and its promise already resolved.
                        return;
                    }

                    if (!capturedAsyncWaiter.Resolved)
                    {
                        capturedWaiters.RemoveAsync(capturedWaiterList, capturedAsyncWaiter);
                        capturedAsyncWaiter.Resolve("timed-out");
                    }
                }, timeoutToken);
            }
        }
        else
        {
            // No buffer data - resolve immediately with "timed-out"
            _engine.AddToEventLoop(() =>
            {
                promiseCapability.Resolve(new JsString("timed-out"));
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
    [JsFunction]
    private JsValue Xor(JsValue thisObject, JsValue typedArray, JsValue index, JsValue value)
    {
        return AtomicReadModifyWrite(typedArray, index, value, AtomicOperation.Xor);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-validateintegertypedarray
    /// </summary>
    private IntrinsicTypedArrayPrototype.TypedArrayWithBufferWitnessRecord ValidateIntegerTypedArray(JsValue typedArray, bool waitable = false, bool requireShared = false, bool isWrite = false)
    {
        var taRecord = typedArray.ValidateTypedArray(_realm, ArrayBufferOrder.Unordered, isWrite: isWrite);
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
        var taRecord = ValidateIntegerTypedArray(typedArrayValue, isWrite: true);
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

    // Interlocked.And/Or are only available in .NET 5.0+; for older frameworks these use
    // CompareExchange loops. Deliberately kept as local helpers rather than routed through
    // extension-member backfills of Interlocked: Interlocked.Xor has no BCL equivalent on any
    // framework, so backfilling only two of the three would split a trio that reads as one unit.
#if NET8_0_OR_GREATER
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
