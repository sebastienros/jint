using System.Globalization;
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

    public override object ToObject()
    {
        Throw.NotImplementedException();
        return null;
    }

    public abstract bool TryIteratorStep(out ObjectInstance nextItem);

    /// <summary>
    /// GetIterator (https://tc39.es/ecma262/#sec-getiterator) step 3 reads <c>next</c> off the object
    /// the @@iterator method handed back, and every step then calls what that read produced. A
    /// built-in iterator may only be stepped natively — skipping both the read and the call — while
    /// the read would resolve to the very built-in function its native stepping implements.
    /// <see cref="JsValue.TryGetIterator"/> asks this before adopting an instance as the iterator
    /// record; a <see langword="false"/> answer wraps it in an <see cref="ObjectIterator"/>, which
    /// performs the read and drives the iteration through the replacement.
    /// The default is <see langword="true"/>: an iterator whose <c>next</c> the engine cannot observe
    /// being replaced keeps stepping natively.
    /// </summary>
    internal virtual bool HasNativeNext => true;

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

    public virtual void Close(CompletionType completion)
    {
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
    /// applies only when the caller's own completion is a throw completion, and such a completion
    /// never reaches this method.
    /// </para>
    /// </summary>
    internal virtual bool TryStartAsyncClose(out JsValue innerResult)
    {
        innerResult = Undefined;
        return false;
    }

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
        /// https://tc39.es/ecma262/#sec-iteratorclose
        /// </summary>
        public override void Close(CompletionType completion)
        {
            // 7.4.11 IteratorClose ( iteratorRecord, completion )
            // Step 3: Let innerResult be Completion(GetMethod(iterator, "return")).
            ICallable? callable;
            try
            {
                callable = _target.GetMethod(CommonProperties.Return);
            }
            catch when (completion == CompletionType.Throw)
            {
                // Step 5: If completion is a throw completion, return ? completion.
                return;
            }

            if (callable is null)
            {
                return;
            }

            JsValue innerResult;
            try
            {
                innerResult = callable.Call(_target, Arguments.Empty);
            }
            catch when (completion == CompletionType.Throw)
            {
                // Step 5: If completion is a throw completion, return ? completion.
                return;
            }

            if (completion != CompletionType.Throw && !innerResult.IsObject())
            {
                Throw.TypeError(_target.Engine.Realm, "Iterator returned non-object");
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-asynciteratorclose steps 3 and 4.a-4.c.
        /// </summary>
        internal override bool TryStartAsyncClose(out JsValue innerResult)
        {
            // Step 3: Let innerResult be Completion(GetMethod(iterator, "return")).
            var callable = _target.GetMethod(CommonProperties.Return);
            if (callable is null)
            {
                // Step 4.b: If return is undefined, return ? completion.
                innerResult = Undefined;
                return false;
            }

            // Step 4.c: Set innerResult to Completion(Call(return, iterator)).
            innerResult = callable.Call(_target, Arguments.Empty);
            return true;
        }
    }

    internal sealed class StringIterator : IteratorInstance
    {
        private readonly TextElementEnumerator _iterator;

        public StringIterator(Engine engine, string str) : base(engine)
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
