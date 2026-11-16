using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-%intlsegmentsprototype%-object - %IntlSegmentsPrototype%, the prototype of every Segments
/// object its realm creates (https://tc39.es/ecma402/#sec-createsegmentsobject), an ordinary object whose own
/// [[Prototype]] is %Object.prototype%. It holds <c>containing</c> and <c>@@iterator</c>, which a Segments object does not
/// have of its own (sebastienros/jint#4452), and has no <c>constructor</c> and no <c>@@toStringTag</c>.
/// <para>
/// A function's realm is the realm of the prototype that holds it, so what each creates is attributed to it: the segment
/// data objects of <c>containing</c>, and the segment iterator of <c>@@iterator</c> with its
/// %IntlSegmentIteratorPrototype%, whichever realm the Segments object being called on comes from.
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class SegmentsPrototype : Prototype
{
    internal SegmentsPrototype(Engine engine, Realm realm, ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// Step 2 of both methods: RequireInternalSlot(segments, [[SegmentsSegmenter]]), a TypeError of this realm.
    /// </summary>
    private JsSegments ValidateSegments(JsValue thisObject)
    {
        if (thisObject is JsSegments segments)
        {
            return segments;
        }

        Throw.TypeError(_realm, "Value is not an Intl.Segmenter segments object");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%intlsegmentsprototype%.containing
    /// </summary>
    [JsFunction]
    private JsValue Containing(JsValue thisObject, JsValue index)
    {
        var segments = ValidateSegments(thisObject);
        var n = TypeConverter.ToIntegerOrInfinity(index);
        return segments.Containing(_realm, n);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-%intlsegmentsprototype%-@@iterator, step 5's CreateSegmentIterator
    /// (https://tc39.es/ecma402/#sec-createsegmentiterator) creating the iterator from this function's realm's
    /// %IntlSegmentIteratorPrototype%.
    /// </summary>
    [JsSymbolFunction("Iterator", Flags = PropertyFlag.Writable | PropertyFlag.Configurable)]
    private JsValue GetIterator(JsValue thisObject)
    {
        var segments = ValidateSegments(thisObject);
        return segments.CreateIterator(_realm.Intrinsics.SegmentIteratorPrototype);
    }
}
