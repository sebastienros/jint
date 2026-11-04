using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Native.Generator;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Runtime;

namespace Jint.Native.Iterator;

internal abstract class IteratorInstance : ObjectInstance
{
    protected IteratorInstance(Engine engine) : base(engine)
    {
        _prototype = engine.Realm.Intrinsics.ArrayIteratorPrototype;
    }

    private protected IteratorInstance(Engine engine, ObjectInstance prototype) : base(engine)
    {
        _prototype = prototype;
    }

    public override object ToObject()
    {
        Throw.NotImplementedException();
        return null;
    }

    public abstract bool TryIteratorStep(out ObjectInstance nextItem);

    /// <summary>
    /// GetIteratorFromMethod (https://tc39.es/ecma262/#sec-getiteratorfrommethod) step 3, GetIteratorDirect steps 1-2,
    /// for a built-in iterator the @@iterator method handed back: read <c>next</c> off it once, and build the iterator
    /// record around what was read, which every step then calls. Answers this instance when the read found the very
    /// built-in function its native stepping implements, so the record steps it natively and skips both the read and
    /// the calls; otherwise an <see cref="ObjectIterator"/> that drives the iteration through the replacement.
    /// <see cref="JsValue.TryGetIterator"/> asks this of every built-in iterator it is handed.
    /// <para>
    /// The default asks the instance's prototype, which captured its built-in <c>next</c> when it was set up
    /// (<see cref="IteratorPrototype.ToIteratorRecord"/>). For an instance with no own property the answer needs
    /// no property read: a cached descriptor and the prototype's own-property version give it, and the
    /// <see cref="ObjectIterator"/> then makes the one read. A prototype that is not a built-in iterator prototype,
    /// or that captured nothing, gets the <see cref="ObjectIterator"/>: slower, never wrong. Where the answer did
    /// take the read, the <see cref="ObjectIterator"/> is handed what it read rather than reading again, since a
    /// getter or a Proxy trap sees every read (sebastienros/jint#4408). Until sebastienros/jint#4392 the default
    /// adopted every instance, so a replaced <c>next</c> on any iterator but an array's was never called.
    /// </para>
    /// </summary>
    internal virtual IteratorInstance ToIteratorRecord()
        => _prototype is IteratorPrototype prototype ? prototype.ToIteratorRecord(this) : WrapReadingNext(this);

    /// <summary>
    /// GetIterator(obj, sync) (https://tc39.es/ecma262/#sec-getiterator) for a built-in iterator object itself - the
    /// operand of <c>for (x of arr.values())</c>, a spread of <c>map.keys()</c>. Steps 1-2 read <c>@@iterator</c> and
    /// call it, and GetIteratorFromMethod (https://tc39.es/ecma262/#sec-getiteratorfrommethod) step 3 reads
    /// <c>next</c> off what it answered. While the instance has no own symbol-keyed property and its prototype's
    /// lookup finds %Iterator.prototype%'s own built-in <c>@@iterator</c>, which answers its receiver, neither of the
    /// first two is observable, and the record is what the cheap <c>next</c> check answers: neither check reads a
    /// property (<see cref="IteratorPrototype.InheritsOriginalIteratorMethod"/>,
    /// <see cref="IteratorPrototype.ToIteratorRecord"/>). Anything else takes the specified path, which calls a
    /// replaced <c>@@iterator</c> and throws for a missing or unusable one. Until sebastienros/jint#4405 a sync
    /// <c>for…of</c> adopted the instance as its record without either read, so it ignored both.
    /// </summary>
    internal override bool TryGetIterator(
        Realm realm,
        [NotNullWhen(true)] out IteratorInstance? iterator,
        GeneratorKind hint = GeneratorKind.Sync,
        ICallable? method = null)
    {
        if (hint == GeneratorKind.Sync
            && method is null
            && _symbols is null
            && _prototype is IteratorPrototype prototype
            && prototype.InheritsOriginalIteratorMethod())
        {
            // The prototype's check, not the virtual ToIteratorRecord(): this skips the ArrayIterator and
            // ArrayLikeIterator overrides, which wrap an iterator whose prototype is not %ArrayIteratorPrototype%.
            // Native stepping is still the same answer only because no built-in `next` brand-checks its receiver, so
            // another kind's untouched `next` steps this iterator exactly as the native lane does. A built-in `next`
            // that starts brand-checking must make this lane check the prototype's kind too.
            iterator = prototype.ToIteratorRecord(this);
            return true;
        }

        return base.TryGetIterator(realm, out iterator, hint, method);
    }

    /// <summary>
    /// The record for an iterator <see cref="ToIteratorRecord"/> may not step natively and has not read <c>next</c>
    /// off: the <see cref="ObjectIterator"/> makes GetIteratorFromMethod's one read. Out of line, so the lanes that
    /// adopt the iterator keep the size they had.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ObjectIterator WrapReadingNext(ObjectInstance iterator) => new(iterator);

    /// <summary>
    /// The record for an iterator whose <c>next</c> <see cref="ToIteratorRecord"/> has already read and found
    /// replaced: the <see cref="ObjectIterator"/> holds what was read and does not read again.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ObjectIterator WrapReplacedNext(ObjectInstance iterator, JsValue next) => new(iterator, next);

    /// <summary>
    /// The iterator record's [[Done]] field, https://tc39.es/ecma262/#sec-iterator-records.
    /// <para>
    /// IteratorNext (steps 3.a and 6.a), IteratorStep (steps 3.a and 5.a) and IteratorStepValue
    /// (step 4.a) set it for <em>every</em> abrupt completion the step itself produces —
    /// <c>next()</c> throwing, <c>next()</c> answering a non-object, and the <c>done</c>/<c>value</c>
    /// reads — plus for the step that reports done. Callers propagate such a completion with
    /// <c>?</c>, so IteratorClose is never reached for any of them; <see cref="CloseIfNotDone"/> is
    /// how that reads here. Only the consumer's own abrupt completion closes.
    /// </para>
    /// </summary>
    internal bool Done { get; private set; }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorstepvalue
    /// Steps the iterator, produces the step's value and maintains <see cref="Done"/>. Returns
    /// <c>false</c> for the spec's ~done~ result. Every consumer that loops an iterator and closes it
    /// on failure should step through this and close through <see cref="CloseIfNotDone"/>, so that
    /// "abrupt while stepping" and "abrupt while processing" cannot be confused — and so that the
    /// distinction is re-established on every iteration rather than only on the first.
    /// </summary>
    internal bool TryIteratorStepValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? value)
    {
        try
        {
            if (!TryStepValue(out value))
            {
                // IteratorStep step 5.a: a done result sets [[Done]] as well.
                Done = true;
                return false;
            }
        }
        catch
        {
            Done = true;
            throw;
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorclose
    /// IteratorClose, guarded by the record's [[Done]] — the shape the spec writes as
    /// "If <c>iteratorRecord</c>.[[Done]] is <c>false</c>, ... IteratorClose". A step that already
    /// failed (or already reported done) leaves nothing to close.
    /// </summary>
    internal void CloseIfNotDone(CompletionType completion)
    {
        if (!Done)
        {
            Close(completion);
        }
    }

    /// <summary>
    /// Rewinds [[Done]] for an instance that is being reused as a fresh iterator record.
    /// </summary>
    private void ResetDone() => Done = false;

    /// <summary>
    /// Steps the iterator and hands back the value directly, letting concrete iterators skip
    /// the per-step IteratorResult object when nothing user-visible needs it (for-in keys).
    /// The default wraps <see cref="TryIteratorStep"/> with exactly the read the for-in/of
    /// loop used to perform, so behavior is unchanged for every other iterator.
    /// </summary>
    internal virtual bool TryStepValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? value)
    {
        if (!TryIteratorStep(out var result))
        {
            value = null;
            return false;
        }

        value = result.Get(CommonProperties.Value);
        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratornext
    /// IteratorNext with an optional value argument, using the cached [[NextMethod]].
    /// </summary>
    public virtual ObjectInstance IteratorNext(JsValue? value = null)
    {
        // Default implementation for built-in iterators that don't support value passing
        TryIteratorStep(out var result);
        return result;
    }

    /// <summary>
    /// The cached [[NextMethod]] from the iterator record.
    /// Returns null for built-in iterators that don't have a cached callable.
    /// </summary>
    public virtual ICallable? NextMethod => null;

    /// <summary>
    /// IteratorNext step 1 (https://tc39.es/ecma262/#sec-iteratornext) as the async consumers take it: call the record's
    /// [[NextMethod]] on its [[Iterator]] and answer the result unchecked, since a <c>for await</c> loop and
    /// Array.fromAsync await it before they look at it. Neither may read <c>next</c> again: the record holds what
    /// GetIteratorFromMethod read once (sebastienros/jint#4410).
    /// <para>
    /// This record steps natively, which <see cref="ToIteratorRecord"/> allowed because <c>next</c> resolved to the
    /// built-in function the prototype captured. That function is the [[NextMethod]], and all it does is this step
    /// (<see cref="IteratorPrototype.Next"/>), so a <c>next</c> the script assigns afterwards is never reached.
    /// </para>
    /// </summary>
    internal virtual JsValue CallNextMethod()
    {
        TryIteratorStep(out var result);
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorclose
    /// IteratorClose for the record this built-in iterator is, which a consumer steps natively: GetMethod reads
    /// <c>return</c> off the iterator (step 3), and a <c>return</c> found is called (step 4.c). No built-in iterator
    /// prototype has one, so a <c>return</c> is one a script added somewhere on the chain. After it, or in its place
    /// when there is none (step 4.b), the close releases whatever native state the iterator holds
    /// (<see cref="CloseNativeState"/>).
    /// <para>
    /// The read is skipped while it cannot find anything: the iterator has no own property, and its prototype's cached
    /// lookup still finds no <c>return</c> on the chain (<see cref="IteratorPrototype.InstancesInheritNoReturnMethod"/>).
    /// That is every close of an iterator nobody touched, the common case on every <c>break</c>, and it reads no
    /// property. Until sebastienros/jint#4406 the close was empty, so a script's <c>return</c> was never called.
    /// </para>
    /// </summary>
    public virtual void Close(CompletionType completion)
    {
        if (InheritsNoReturnMethod())
        {
            CloseNativeState(completion);
            return;
        }

        IteratorClose(this, completion);
    }

    /// <summary>
    /// Whether GetMethod(this, "return") answers <c>undefined</c> without reading anything: no own property can hold
    /// it, and the prototype's cached lookup found none above. Anything else, a Proxy or a host object on the chain
    /// included, is <see langword="false"/>, and the caller reads <c>return</c> as specified.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool InheritsNoReturnMethod()
        => _properties is null && _prototype is IteratorPrototype prototype && prototype.InstancesInheritNoReturnMethod();

    /// <summary>
    /// The native state a built-in iterator holds, released when the iterator is closed: a wrapped CLR collection's
    /// iterator disposes its host enumerator here, and every other built-in iterator holds nothing. IteratorClose
    /// reaches it whether or not it found a <c>return</c> to call. Without one there is nothing else to do (step 4.b);
    /// with one it runs after the <c>return</c>, however that ended, since the consumer is done with the iterator either
    /// way and a script's <c>return</c> has no means of releasing the host's enumerator itself. A host exception it
    /// raises is swallowed only when <paramref name="completion"/>, the completion leaving the close, is a throw, which
    /// then wins (IteratorClose step 5).
    /// </summary>
    private protected virtual void CloseNativeState(CompletionType completion)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorclose steps 3-7 for <paramref name="iterator"/>, with the
    /// <paramref name="completion"/> the consumer leaves with. Under a throw completion nothing the close raises
    /// replaces it (step 5); otherwise what <c>return</c> throws propagates, and an answer that is not an object is a
    /// <c>TypeError</c> (step 7). A built-in iterator's native state is released last, whatever happened
    /// (<see cref="CloseNativeState"/>), and is told a throw completion whenever a throw is leaving the close, so that a
    /// host's <c>Dispose</c> cannot replace it. Out of line: a natively stepped record reaches it only once a script has
    /// added a <c>return</c> somewhere.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void IteratorClose(ObjectInstance iterator, CompletionType completion)
    {
        // The completion leaving the close, as the native release is told it: a throw until the steps below are known
        // to have completed.
        var leaving = CompletionType.Throw;
        try
        {
            // Step 3: Let innerResult be Completion(GetMethod(iterator, "return")).
            ICallable? callable;
            try
            {
                callable = iterator.GetMethod(CommonProperties.Return);
            }
            catch when (completion == CompletionType.Throw)
            {
                // Step 5: If completion is a throw completion, return ? completion.
                return;
            }

            // Step 4.b: with no `return` there is nothing to call, and only the native release below is left.
            if (callable is not null)
            {
                JsValue innerResult;
                try
                {
                    innerResult = callable.Call(iterator, Arguments.Empty);
                }
                catch when (completion == CompletionType.Throw)
                {
                    // Step 5: If completion is a throw completion, return ? completion.
                    return;
                }

                if (completion != CompletionType.Throw && !innerResult.IsObject())
                {
                    Throw.TypeError(iterator.Engine.Realm, "Iterator returned non-object");
                }
            }

            leaving = completion;
        }
        finally
        {
            // A built-in iterator - the record itself, or the one an ObjectIterator wraps because its `next` was replaced
            // or GetIteratorDirect took it for an iterator helper - releases its native state, such as a host enumerator
            // to dispose, after the `return` or in its place (sebastienros/jint#4406).
            CloseNatively(iterator, leaving);
        }
    }

    /// <summary>
    /// IteratorClose step 4.b (https://tc39.es/ecma262/#sec-iteratorclose), AsyncIteratorClose step 4.b
    /// (https://tc39.es/ecma262/#sec-asynciteratorclose) and %AsyncFromSyncIteratorPrototype%.return step 8
    /// (https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.return), for a caller that has read
    /// <c>return</c> itself and found none: nothing script-visible happens, and a built-in iterator releases the native
    /// state only its own <see cref="CloseNativeState"/> can. This reaches that method from every record around a
    /// built-in iterator: the iterator itself, an <see cref="ObjectIterator"/> built because its <c>next</c> was
    /// replaced (<see cref="ToIteratorRecord"/>) or by GetIteratorDirect for an iterator helper, and the sync record
    /// behind a CreateAsyncFromSyncIterator wrapper, whichever of the two that is. It never reads <c>return</c> again.
    /// </summary>
    internal void CloseWithoutReturn(CompletionType completion) => CloseNatively(Instance, completion);

    /// <summary>
    /// The native release an asynchronous close owes once the <c>return</c> it called has settled
    /// (https://tc39.es/ecma262/#sec-asynciteratorclose step 4.d): after the Await rather than at the call, so that a
    /// <c>return</c> still using the iterator until its promise settles finds it whole. A consumer that awaits nothing
    /// releases it as soon as the call returns. <paramref name="leaving"/> is the completion the close ends with: a
    /// throw whenever steps 5-7 leave by one. A <c>for await</c> over a sync iterable holds the
    /// CreateAsyncFromSyncIterator wrapper, and the release reaches the sync iterator behind it. <c>yield*</c> releases
    /// its delegate this way when a return() it forwards ends the delegation (sebastienros/jint#4429).
    /// </summary>
    internal void CloseNativeStateAfterReturn(CompletionType leaving) => CloseNatively(Instance, leaving);

    private static void CloseNatively(ObjectInstance iterator, CompletionType completion)
    {
        // A CreateAsyncFromSyncIterator wrapper holds no native state of its own; its sync iterator may.
        if (iterator is AsyncFromSyncIterator wrapper)
        {
            iterator = wrapper.SyncIterator.Instance;
        }

        if (iterator is IteratorInstance native and not ObjectIterator)
        {
            native.CloseNativeState(completion);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asynciteratorclose
    /// AsyncIteratorClose steps 3 and 4.a-4.c: read the iterator's <c>return</c> method and call it,
    /// handing back the value whose Await — step 4.d — the caller still owes. Answers
    /// <see langword="false"/> for step 4.b ("If return is undefined, return ? completion"), where
    /// there is nothing to await and nothing to check.
    /// <para>
    /// Step 4.d is deliberately not performed here: awaiting suspends the surrounding async function
    /// or async generator, which only the interpreter can do. The caller resumes on the settlement
    /// and performs steps 5-8 itself, which is why this is split out of <see cref="Close"/> rather
    /// than being another completion type <see cref="Close"/> understands.
    /// </para>
    /// <para>
    /// An abrupt completion of either step propagates. Step 5 — the one that would swallow it —
    /// applies only when the caller's own completion is a throw completion, and a caller holding one
    /// catches it there and skips the Await (<c>JintForInForOfStatement.TryStartAsyncCloseDiscardingAbrupt</c>).
    /// </para>
    /// <para>
    /// This record is a built-in iterator stepped natively, the loop's record when a script's <c>@@asyncIterator</c>
    /// answered one. It reads <c>return</c> exactly as <see cref="Close"/> does, skipping the read on the same cached
    /// answer, and with none releases its native state. With one, the native state is released once what it answered
    /// has settled (<see cref="CloseNativeStateAfterReturn"/>, which the caller owes), or at once if reading or calling
    /// it throws. Until sebastienros/jint#4406 it released that state without reading <c>return</c>, so a script's
    /// <c>return</c> on the iterator was never called.
    /// </para>
    /// </summary>
    internal virtual bool TryStartAsyncClose(out JsValue innerResult)
    {
        if (InheritsNoReturnMethod())
        {
            CloseNativeState(CompletionType.Normal);
            innerResult = Undefined;
            return false;
        }

        return TryStartAsyncClose(this, out innerResult);
    }

    /// <summary>
    /// AsyncIteratorClose steps 3 and 4.a-4.c (https://tc39.es/ecma262/#sec-asynciteratorclose) for
    /// <paramref name="iterator"/>; see <see cref="TryStartAsyncClose(out JsValue)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryStartAsyncClose(ObjectInstance iterator, out JsValue innerResult)
    {
        // Step 3: Let innerResult be Completion(GetMethod(iterator, "return")).
        ICallable? callable;
        try
        {
            callable = iterator.GetMethod(CommonProperties.Return);
        }
        catch
        {
            ReleaseOnAbruptStart(iterator);
            throw;
        }

        if (callable is null)
        {
            // Step 4.b: If return is undefined, return ? completion. A built-in iterator still releases its native
            // state, as a sync close does at the same step.
            CloseNatively(iterator, CompletionType.Normal);
            innerResult = Undefined;
            return false;
        }

        // Step 4.c: Set innerResult to Completion(Call(return, iterator)). The caller awaits it, and releases the native
        // state once it has settled (CloseNativeStateAfterReturn).
        try
        {
            innerResult = callable.Call(iterator, Arguments.Empty);
        }
        catch
        {
            ReleaseOnAbruptStart(iterator);
            throw;
        }

        return true;
    }

    /// <summary>
    /// Reading or calling <c>return</c> threw, so the close has nothing to await and that throw is what leaves it: the
    /// native state is released at once, told a throw completion, so that a host's <c>Dispose</c> cannot replace it.
    /// </summary>
    private static void ReleaseOnAbruptStart(ObjectInstance iterator) => CloseNatively(iterator, CompletionType.Throw);

    /// <summary>
    /// Gets the underlying iterator object instance.
    /// For object iterators, this is the wrapped object. For built-in iterators, this is self.
    /// Used by yield* to call methods like "return" and "throw" on the iterator.
    /// </summary>
    public virtual ObjectInstance Instance => this;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createiterresultobject
    /// </summary>
    private IteratorResult CreateIterResultObject(JsValue value, bool done)
    {
        return new IteratorResult(_engine, value, JsBoolean.Create(done));
    }

    internal sealed class ObjectIterator : IteratorInstance
    {
        private readonly ObjectInstance _target;
        private readonly ICallable? _nextMethod;

        public override ObjectInstance Instance => _target;
        public override ICallable? NextMethod => _nextMethod;

        public ObjectIterator(ObjectInstance target) : base(target.Engine)
        {
            _target = target;
            // Don't check for 'next' method here - it's only required when actually iterating
            // This allows iterators with only 'return' method to be created (e.g., for closing)
            if (target.Get(CommonProperties.Next) is ICallable callable)
            {
                _nextMethod = callable;
            }
        }

        /// <summary>
        /// The record GetIteratorDirect step 2 builds around <paramref name="nextMethod"/>, the value its step 1 read
        /// off <paramref name="target"/> (GetIteratorFromMethod step 3,
        /// https://tc39.es/ecma262/#sec-getiteratorfrommethod). For a caller that has already made that read, so that a
        /// getter or a Proxy trap sees it once (sebastienros/jint#4408). Kept apart from the constructor above rather
        /// than chained from it: chaining made the JIT inline that one into every caller,
        /// <see cref="JsValue.TryGetIterator"/> among them, which grew the method the array lane runs in.
        /// </summary>
        internal ObjectIterator(ObjectInstance target, JsValue nextMethod) : base(target.Engine)
        {
            _target = target;
            _nextMethod = nextMethod as ICallable;
        }

        public override bool TryIteratorStep(out ObjectInstance result)
        {
            result = IteratorNextInternal(null);

            var done = result.Get(CommonProperties.Done);
            if (!done.IsUndefined() && TypeConverter.ToBoolean(done))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-iteratornext
        /// Uses the cached [[NextMethod]] and forwards the optional value argument.
        /// </summary>
        public override ObjectInstance IteratorNext(JsValue? value = null)
        {
            return IteratorNextInternal(value);
        }

        private ObjectInstance IteratorNextInternal(JsValue? value)
        {
            // Check for 'next' method when actually trying to iterate
            if (_nextMethod is null)
            {
                Throw.TypeError(_target.Engine.Realm, "Iterator does not have a next method");
                return null!;
            }

            var jsValue = value is not null
                ? _nextMethod.Call(_target, [value])
                : _nextMethod.Call(_target, Arguments.Empty);
            var instance = jsValue as ObjectInstance;
            if (instance is null)
            {
                Throw.TypeError(_target.Engine.Realm, $"Iterator result {jsValue} is not an object");
            }

            return instance;
        }

        /// <summary>
        /// IteratorNext step 1 through the [[NextMethod]] this record captured, never a fresh read of <c>next</c>; a
        /// value that was not callable fails here, as the Call would.
        /// </summary>
        internal override JsValue CallNextMethod()
        {
            if (_nextMethod is null)
            {
                Throw.TypeError(_target.Engine.Realm, "Iterator does not have a next method");
                return null!;
            }

            return _nextMethod.Call(_target, Arguments.Empty);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-iteratorclose, always reading <c>return</c> off the object this record holds.
        /// </summary>
        public override void Close(CompletionType completion) => IteratorClose(_target, completion);

        /// <summary>
        /// https://tc39.es/ecma262/#sec-asynciteratorclose steps 3 and 4.a-4.c, always reading <c>return</c> off the
        /// object this record holds.
        /// </summary>
        internal override bool TryStartAsyncClose(out JsValue innerResult) => TryStartAsyncClose(_target, out innerResult);
    }

    internal sealed class StringIterator : IteratorInstance
    {
        private readonly TextElementEnumerator _iterator;

        // Both StringIteratorPrototype lanes hand in themselves: the iterator a string primitive's lane builds natively
        // stands in for the CreateStringIterator object (https://tc39.es/ecma262/#sec-createstringiterator), so closing
        // it finds a `return` where that object's close would - on %StringIteratorPrototype% or above it, never on the
        // %ArrayIteratorPrototype% the base constructor assigns (sebastienros/jint#4406).
        public StringIterator(Engine engine, string str, ObjectInstance prototype) : base(engine, prototype)
        {
            _iterator = StringInfo.GetTextElementEnumerator(str);
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_iterator.MoveNext())
            {
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, (string) _iterator.Current);
                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    internal sealed class RegExpStringIterator : IteratorInstance
    {
        private readonly JsRegExp _iteratingRegExp;
        private readonly string _s;
        private readonly bool _global;
        private readonly bool _unicode;

        private bool _done;

        public RegExpStringIterator(Engine engine, ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode) : base(engine)
        {
            var r = iteratingRegExp as JsRegExp;
            if (r is null)
            {
                Throw.TypeError(engine.Realm);
            }

            _iteratingRegExp = r;
            _s = iteratedString;
            _global = global;
            _unicode = unicode;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_done)
            {
                nextItem = CreateIterResultObject(Undefined, true);
                return false;
            }

            var match = RegExpPrototype.RegExpExec(_iteratingRegExp, _s);
            if (match.IsNull())
            {
                _done = true;
                nextItem = CreateIterResultObject(Undefined, true);
                return false;
            }

            if (_global)
            {
                var macthStr = TypeConverter.ToString(match.Get(JsString.NumberZeroString));
                if (macthStr == "")
                {
                    var thisIndex = TypeConverter.ToLength(_iteratingRegExp.Get(JsRegExp.PropertyLastIndex));
                    var nextIndex = RegExpPrototype.AdvanceStringIndex(_s, thisIndex, _unicode);
                    _iteratingRegExp.Set(JsRegExp.PropertyLastIndex, nextIndex, true);
                }
            }
            else
            {
                _done = true;
            }

            nextItem = CreateIterResultObject(match, false);
            return true;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-enumerate-object-properties
    /// for-in key enumerator: walks own string keys level by level up the prototype chain,
    /// probing presence and enumerability per key at step time (a key deleted before its turn
    /// is skipped). Keys seen as present on shallower levels are tracked in a plain list; the
    /// hash set that shadow-filters deeper levels materializes only when a deeper level
    /// actually produces a present key — enumerating an object whose prototypes contribute
    /// nothing (the common case: every Object.prototype / Array.prototype member is
    /// non-enumerable... but present, so they still probe) allocates no per-step result
    /// objects and no nested per-level iterator machinery.
    /// </summary>
    internal sealed class ForInIterator : IteratorInstance
    {
        private ObjectInstance? _current;
        private IReadOnlyList<JsValue> _keys;
        private int _index;
        private bool _deeperLevel;
        private List<CompletedLevel>? _completedLevels;
        private HashSet<JsValue>? _visited;

        // Array index mode: a dense JsArray with no named own props whose prototype chain provably
        // contributes no enumerable keys enumerates its present indices lazily — no key list, no
        // per-step string->index parse, and (indices < 1024) no per-key JsString. The chain was
        // clean at head evaluation, so the enumeration ends when the index range is exhausted; the
        // level/shadowing machinery below never engages. Everything else takes the exact path.
        private JsArray? _fastArray;
        private uint _fastArrayBound;
        private uint _fastArrayIndex;

        // Keys of the level currently being walked that probed Missing at their turn (deleted or
        // absent before they were reached). Usually null — populated only when a key vanishes
        // mid-enumeration. A shadow set, when it is later built, must treat these as never-present:
        // a key visited then deleted still shadows deeper levels, but a key deleted before its turn
        // does not — a distinction a live re-probe cannot make (both read Missing afterwards).
        private HashSet<JsValue>? _levelAbsent;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
        private readonly record struct CompletedLevel(IReadOnlyList<JsValue> Keys, HashSet<JsValue>? Absent);

        public ForInIterator(Engine engine, ObjectInstance target) : base(engine)
        {
            _keys = System.Array.Empty<JsValue>();
            InitializeEnumeration(target);
        }

        /// <summary>
        /// The iterator EnumerateObjectProperties answers is never closed: for the enumerate kind,
        /// ForIn/OfBodyEvaluation returns its result before it reaches IteratorClose
        /// (https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset).
        /// Nothing reads a <c>return</c>, however the prototype this instance happens to inherit is dressed.
        /// </summary>
        public override void Close(CompletionType completion)
        {
        }

        private void InitializeEnumeration(ObjectInstance target)
        {
            if (target is JsArray array
                && array.TryStartForInIndexMode(out var bound)
                && PrototypeChainHasNoEnumerableStringKeys(array))
            {
                _fastArray = array;
                _fastArrayBound = bound;
                _fastArrayIndex = 0;
                _current = null;
                _keys = System.Array.Empty<JsValue>();
                return;
            }

            _fastArray = null;
            _current = target;
            // matches the previous lazy enumerator's first step closely enough: no user code
            // can observe between head evaluation and the first step, so the ownKeys order
            // for proxies is preserved; GetPrototypeOf is deliberately NOT consulted here
            // (a proxy trap must not fire before the first level is exhausted). Shape/builtin-shape
            // objects hand back a shared, memoized key array (no per-entry List/JsString allocation);
            // exotic objects (proxies) still route through the ownKeys-trapping GetOwnPropertyKeys.
            _keys = target.GetForInStringKeys();
        }

        /// <summary>
        /// True when every object on <paramref name="target"/>'s prototype chain proves it has no
        /// enumerable own string keys. Walks the raw prototype fields: an object is only advanced
        /// through after it passed the check, and everything that can pass is ordinary (a proxy's
        /// GetPrototypeOf trap can therefore never fire here — the proxy itself fails the check
        /// first).
        /// </summary>
        private static bool PrototypeChainHasNoEnumerableStringKeys(ObjectInstance target)
        {
            var proto = target._prototype;
            while (proto is not null)
            {
                if (!proto.HasNoEnumerableOwnStringKeys())
                {
                    return false;
                }

                proto = proto._prototype;
            }

            return true;
        }

        internal override bool TryStepValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? value)
        {
            var fastArray = _fastArray;
            if (fastArray is not null)
            {
                if (fastArray.TryFastForInStep(ref _fastArrayIndex, _fastArrayBound, out value))
                {
                    return true;
                }

                // exhausted; the prototype chain was clean at head evaluation, so nothing deeper
                // can contribute — the enumeration is complete
                _fastArray = null;
                return false;
            }

            while (_current is not null)
            {
                var current = _current;
                var keys = _keys;
                while (_index < keys.Count)
                {
                    var key = keys[_index++];
                    var probe = current.ProbeOwnPropertyChecked(key);
                    if (probe == OwnPropertyProbe.Missing)
                    {
                        // absent before its turn — not visited, does not shadow. Record it so a
                        // shadow set built later reconstructs this level's membership from the
                        // retained key list minus these, rather than re-probing the mutated object.
                        if (_visited is null)
                        {
                            (_levelAbsent ??= []).Add(key);
                        }
                        continue;
                    }

                    if (_deeperLevel)
                    {
                        if (_visited is null)
                        {
                            if (probe != OwnPropertyProbe.Enumerable)
                            {
                                // present but non-enumerable: never yields, and its shadowing
                                // of still-deeper levels is reconstructed from the retained
                                // level snapshots if a set is ever needed
                                continue;
                            }

                            _visited = BuildVisited(keys, _index - 1, _levelAbsent);
                        }

                        if (!_visited.Add(key))
                        {
                            continue; // shadowed by a shallower level
                        }
                    }

                    if (probe == OwnPropertyProbe.Enumerable)
                    {
                        value = key;
                        return true;
                    }
                }

                var proto = current.GetPrototypeOf();
                if (proto is null)
                {
                    _current = null;
                    break;
                }

                if (_visited is null)
                {
                    // retain the exhausted level's snapshot (a list reference, no copy) plus any
                    // keys that vanished before their turn, so a later set build reconstructs
                    // exactly what this level shadowed without re-probing the mutated object
                    (_completedLevels ??= []).Add(new CompletedLevel(keys, _levelAbsent));
                }

                _levelAbsent = null;
                _deeperLevel = true;
                _current = proto;
                _keys = proto.GetForInStringKeys();
                _index = 0;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// First enumerable candidate on a deeper level: build the shadow set from every
        /// shallower level's retained snapshot plus the already-processed prefix of the
        /// current level, taking each key that was present when this enumeration reached it
        /// (its snapshot minus the keys recorded absent at their turn). Prototype members are
        /// overwhelmingly non-enumerable, so most enumerations never get here.
        /// </summary>
        private HashSet<JsValue> BuildVisited(IReadOnlyList<JsValue> currentKeys, int processedCount, HashSet<JsValue>? currentAbsent)
        {
            var set = new HashSet<JsValue>();
            if (_completedLevels is not null)
            {
                foreach (var level in _completedLevels)
                {
                    AddPresentAtTurn(set, level.Keys, level.Keys.Count, level.Absent);
                }

                _completedLevels = null;
            }

            AddPresentAtTurn(set, currentKeys, processedCount, currentAbsent);

            return set;
        }

        // A key shadows deeper levels iff it was present when this level reached it: every key in
        // the retained snapshot except those recorded absent at their turn. Reconstructing from the
        // turn-time record (not a live re-probe) keeps a visited-then-deleted key in the shadow set.
        private static void AddPresentAtTurn(HashSet<JsValue> set, IReadOnlyList<JsValue> keys, int count, HashSet<JsValue>? absent)
        {
            for (var i = 0; i < count; i++)
            {
                var key = keys[i];
                if (absent is null || !absent.Contains(key))
                {
                    set.Add(key);
                }
            }
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (TryStepValue(out var value))
            {
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, value);
                return true;
            }

            nextItem = IteratorResult.CreateValueIteratorPosition(_engine, done: JsBoolean.True);
            return false;
        }

        /// <summary>
        /// True when this iterator was created by <paramref name="engine"/>. A for-in handler is shared
        /// across engines when a <c>Prepared&lt;Script&gt;</c> is reused, so a parked iterator must only be
        /// reused by the engine that owns it.
        /// </summary>
        internal bool BelongsTo(Engine engine) => ReferenceEquals(_engine, engine);

        /// <summary>
        /// Re-arms a parked iterator to enumerate <paramref name="target"/>, reusing (clearing, not
        /// reallocating) the retained level snapshots and shadow set so a pooled instance allocates nothing
        /// per loop entry. Never called on an iterator that is mid-enumeration (the pool hands out at most
        /// one live reference per statement — see JintForInForOfStatement).
        /// </summary>
        internal void ResetForReuse(ObjectInstance target)
        {
            _index = 0;
            _deeperLevel = false;
            _completedLevels?.Clear();
            // Null, not Clear: _visited being null is the "shadow set not built yet" sentinel that
            // drives level-snapshot retention and own-key seeding; a non-null empty set would skip
            // both and let a deeper key shadow-collide on the next enumeration. _levelAbsent is a
            // per-level scratch set carried into CompletedLevel — it must not leak across reuses.
            _visited = null;
            _levelAbsent = null;
            // A pooled instance is a fresh iterator record for the next loop entry, so its [[Done]]
            // starts over too. For-in steps through TryStepValue rather than TryIteratorStepValue and
            // so never sets it today; resetting it keeps that an implementation detail of the caller.
            ResetDone();
            InitializeEnumeration(target);
        }

        /// <summary>
        /// Drops references to the just-finished enumeration's object, keys, level snapshots and shadow set
        /// before the iterator is parked, so a cached instance never roots them. The backing List/HashSet
        /// storage is retained (cleared) for the next reuse.
        /// </summary>
        internal void ClearForPark()
        {
            _current = null;
            _keys = System.Array.Empty<JsValue>();
            _index = 0;
            _deeperLevel = false;
            _completedLevels?.Clear();
            // see ResetForReuse: null is the sentinel, and _levelAbsent must not survive parking
            _visited = null;
            _levelAbsent = null;
            _fastArray = null;
            _fastArrayBound = 0;
            _fastArrayIndex = 0;
        }
    }

    internal sealed class EnumerableIterator : IteratorInstance
    {
        private readonly IEnumerator<JsValue> _enumerable;

        public EnumerableIterator(Engine engine, IEnumerable<JsValue> obj) : base(engine)
        {
            _enumerable = obj.GetEnumerator();
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_enumerable.MoveNext())
            {
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, _enumerable.Current);
                return true;
            }

            nextItem = IteratorResult.CreateValueIteratorPosition(_engine, done: JsBoolean.True);
            return false;
        }
    }

    internal sealed class GeneratorIterator : IteratorInstance
    {
        private readonly GeneratorInstance _generator;

        public GeneratorIterator(Engine engine, GeneratorInstance generator) : base(engine)
        {
            _generator = generator;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            nextItem = IteratorResult.CreateValueIteratorPosition(_engine, done: JsBoolean.True);
            return false;
        }
    }

}
