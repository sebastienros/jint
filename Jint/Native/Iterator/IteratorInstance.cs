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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
        private readonly record struct CompletedLevel(ObjectInstance Owner, IReadOnlyList<JsValue> Keys);

        public ForInIterator(Engine engine, ObjectInstance target) : base(engine)
        {
            _current = target;
            // matches the previous lazy enumerator's first step closely enough: no user code
            // can observe between head evaluation and the first step, so the ownKeys order
            // for proxies is preserved; GetPrototypeOf is deliberately NOT consulted here
            // (a proxy trap must not fire before the first level is exhausted). Shape/builtin-shape
            // objects hand back a shared, memoized key array (no per-entry List/JsString allocation);
            // exotic objects (proxies) still route through the ownKeys-trapping GetOwnPropertyKeys.
            _keys = target.GetForInStringKeys();
        }

        internal override bool TryStepValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? value)
        {
            while (_current is not null)
            {
                var current = _current;
                var keys = _keys;
                while (_index < keys.Count)
                {
                    var key = keys[_index++];
                    var probe = current.ProbeOwnProperty(key);
                    if (probe == OwnPropertyProbe.Missing)
                    {
                        // deleted before its turn — not visited, does not shadow
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

                            _visited = BuildVisited(current, keys, _index - 1);
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
                    // retain the exhausted level's snapshot (a list reference, no copy) so a
                    // later set build can reconstruct what it shadowed
                    (_completedLevels ??= []).Add(new CompletedLevel(current, keys));
                }

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
        /// current level, re-probing each key on its owning object. Prototype members are
        /// overwhelmingly non-enumerable, so most enumerations never get here.
        /// </summary>
        private HashSet<JsValue> BuildVisited(ObjectInstance current, IReadOnlyList<JsValue> currentKeys, int processedCount)
        {
            var set = new HashSet<JsValue>();
            if (_completedLevels is not null)
            {
                foreach (var level in _completedLevels)
                {
                    var levelKeys = level.Keys;
                    for (var i = 0; i < levelKeys.Count; i++)
                    {
                        if (level.Owner.ProbeOwnProperty(levelKeys[i]) != OwnPropertyProbe.Missing)
                        {
                            set.Add(levelKeys[i]);
                        }
                    }
                }

                _completedLevels = null;
            }

            for (var i = 0; i < processedCount; i++)
            {
                if (current.ProbeOwnProperty(currentKeys[i]) != OwnPropertyProbe.Missing)
                {
                    set.Add(currentKeys[i]);
                }
            }

            return set;
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
            _current = target;
            _keys = target.GetForInStringKeys();
            _index = 0;
            _deeperLevel = false;
            _completedLevels?.Clear();
            _visited?.Clear();
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
            _visited?.Clear();
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
