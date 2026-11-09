namespace Jint.Native.Iterator;

/// <summary>
/// The kind of a built-in iterator: which concrete iterator prototype's <c>next</c> may step it. Each of those
/// <c>next</c> functions refuses any other receiver with a <c>TypeError</c>. %ArrayIteratorPrototype%.next
/// (https://tc39.es/ecma262/#sec-%arrayiteratorprototype%.next) and its RegExp-string peer require their own internal
/// slots, the <c>Map</c>, <c>Set</c> and string ones compare a generator brand (https://tc39.es/ecma262/#sec-generatorvalidate),
/// the segment iterator's requires [[IteratingSegmenter]] (https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%.next),
/// and a WebIDL iterator's its interface (https://webidl.spec.whatwg.org/#es-iterator-prototype-object).
/// <para>
/// None of those tests names a realm, so neither does a brand: another realm's prototype of the same kind steps the
/// iterator too. An iterator's brand is fixed when it is created; its prototype is not, which is why the native lane
/// compares the two (<see cref="IteratorPrototype.ToIteratorRecord"/>).
/// </para>
/// </summary>
internal enum IteratorBrand : byte
{
    /// <summary>
    /// %ArrayIteratorPrototype%: the iterators of arrays, typed arrays and array-likes, and every other iterator Jint
    /// gives that prototype - a wrapped CLR collection's, among them. The zero value, so that the
    /// <see cref="IteratorInstance"/> constructor which assigns that prototype has nothing to store.
    /// </summary>
    Array,
    Map,
    Set,
    String,
    RegExpString,
    Segment,
    Headers,
    UrlSearchParams,
    FormData,

    /// <summary>
    /// %Iterator.prototype% itself, which has no <c>next</c>: no iterator carries it.
    /// </summary>
    None,
}
