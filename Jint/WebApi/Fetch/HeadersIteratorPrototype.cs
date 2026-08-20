#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The iterator prototype behind <c>Headers.prototype.entries</c>, <c>keys</c> and <c>values</c> — the object
/// WebIDL calls an "iterator prototype object",
/// https://webidl.spec.whatwg.org/#es-iterator-prototype-object.
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is <c>%IteratorPrototype%</c>, so a headers iterator inherits the whole
/// iterator-helper surface (<c>map</c>, <c>take</c>, <c>toArray</c>, …) exactly as a <c>Map</c> iterator does.
/// </para>
/// <para>
/// Each step recomputes <i>sort and combine</i> over the live header list and indexes into the result, which
/// is what https://webidl.spec.whatwg.org/#es-default-iterator-object specifies: a header appended during
/// iteration is seen, and one deleted shifts the rest under the cursor.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class HeadersIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString HeadersIteratorToStringTag = new("Headers Iterator");

    internal HeadersIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm, iteratorPrototype)
    {
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsFunction(Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    internal IteratorInstance ConstructEntryIterator(JsHeaders headers)
        => new HeadersIterator(_engine, headers, IteratorKind.KeyAndValue) { _prototype = this };

    internal IteratorInstance ConstructKeyIterator(JsHeaders headers)
        => new HeadersIterator(_engine, headers, IteratorKind.Key) { _prototype = this };

    internal IteratorInstance ConstructValueIterator(JsHeaders headers)
        => new HeadersIterator(_engine, headers, IteratorKind.Value) { _prototype = this };

    private enum IteratorKind
    {
        Key,
        Value,
        KeyAndValue,
    }

    private sealed class HeadersIterator : IteratorInstance
    {
        private readonly JsHeaders _headers;
        private readonly IteratorKind _kind;
        private int _index;

        public HeadersIterator(Engine engine, JsHeaders headers, IteratorKind kind) : base(engine)
        {
            _headers = headers;
            _kind = kind;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            var pairs = _headers.List.SortAndCombine();
            if (_index < pairs.Count)
            {
                var pair = pairs[_index];
                _index++;

                nextItem = _kind switch
                {
                    IteratorKind.Key => IteratorResult.CreateValueIteratorPosition(_engine, JsString.Create(pair.LowerName)),
                    IteratorKind.Value => IteratorResult.CreateValueIteratorPosition(_engine, JsString.Create(pair.Value)),
                    _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, JsString.Create(pair.LowerName), JsString.Create(pair.Value)),
                };

                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}
#endif
