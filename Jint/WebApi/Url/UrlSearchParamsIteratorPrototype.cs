#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Url;

/// <summary>
/// The iterator prototype behind <c>URLSearchParams.prototype.entries</c>, <c>keys</c> and <c>values</c> —
/// the object WebIDL calls an "iterator prototype object",
/// https://webidl.spec.whatwg.org/#es-iterator-prototype-object.
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is <c>%IteratorPrototype%</c>, so a search-params iterator inherits the whole
/// iterator-helper surface (<c>map</c>, <c>take</c>, <c>toArray</c>, …) exactly as a <c>Map</c> iterator does.
/// The C# base class is the same one <c>MapIteratorPrototype</c> uses, purely to reuse its <c>next</c>
/// dispatcher.
/// </para>
/// <para>
/// The iterator walks the <b>live</b> list by index, which is what
/// https://webidl.spec.whatwg.org/#default-iterator-object specifies: appending during iteration extends it,
/// and deleting shifts the remaining entries down under the cursor.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class UrlSearchParamsIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString UrlSearchParamsIteratorToStringTag = new("URLSearchParams Iterator");

    internal UrlSearchParamsIteratorPrototype(
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

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-iterator-prototype-object — "an iterator prototype object must
    /// have a <c>next</c> data property with attributes <c>{ [[Writable]]: true, [[Enumerable]]: true,
    /// [[Configurable]]: true }</c>".
    /// </summary>
    /// <remarks>
    /// The attributes are WebIDL's, not ECMA-262's, and enumerable is the one that has to be spelled out: a
    /// built-in function property is non-enumerable everywhere in the language
    /// (https://tc39.es/ecma262/#sec-ecmascript-standard-built-in-objects), which is what
    /// <c>[JsFunction]</c> defaults to. The <c>@@toStringTag</c> below keeps the language's shape, because
    /// a class string is <c>{ [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: true }</c> in
    /// WebIDL too.
    /// </remarks>
    [JsFunction(Name = "next", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    internal IteratorInstance ConstructEntryIterator(JsUrlSearchParams parameters)
        => new UrlSearchParamsIterator(_engine, parameters, IteratorKind.KeyAndValue) { _prototype = this };

    internal IteratorInstance ConstructKeyIterator(JsUrlSearchParams parameters)
        => new UrlSearchParamsIterator(_engine, parameters, IteratorKind.Key) { _prototype = this };

    internal IteratorInstance ConstructValueIterator(JsUrlSearchParams parameters)
        => new UrlSearchParamsIterator(_engine, parameters, IteratorKind.Value) { _prototype = this };

    private enum IteratorKind
    {
        Key,
        Value,
        KeyAndValue,
    }

    private sealed class UrlSearchParamsIterator : IteratorInstance
    {
        private readonly JsUrlSearchParams _parameters;
        private readonly IteratorKind _kind;
        private int _index;

        public UrlSearchParamsIterator(Engine engine, JsUrlSearchParams parameters, IteratorKind kind) : base(engine)
        {
            _parameters = parameters;
            _kind = kind;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            var list = _parameters.List;
            if (_index < list.Count)
            {
                var entry = list[_index];
                _index++;

                nextItem = _kind switch
                {
                    IteratorKind.Key => IteratorResult.CreateValueIteratorPosition(_engine, JsString.Create(entry.Name)),
                    IteratorKind.Value => IteratorResult.CreateValueIteratorPosition(_engine, JsString.Create(entry.Value)),
                    _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, JsString.Create(entry.Name), JsString.Create(entry.Value)),
                };

                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}
#endif
