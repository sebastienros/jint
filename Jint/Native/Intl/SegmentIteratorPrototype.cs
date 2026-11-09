using Jint.Native.Iterator;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%-object - %IntlSegmentIteratorPrototype%, the prototype of
/// every segment iterator its realm creates (https://tc39.es/ecma402/#sec-createsegmentiterator), whose own prototype is
/// %Iterator.prototype%. Until sebastienros/jint#4407 a segment iterator inherited %ArrayIteratorPrototype%, so a
/// <c>next</c> or a <c>return</c> put there reached it.
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class SegmentIteratorPrototype : IteratorPrototype
{
    /// <summary>
    /// https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%.%symbol.tostringtag%
    /// </summary>
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString SegmentIteratorToStringTag = new("Segmenter String Iterator");

    internal SegmentIteratorPrototype(
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
    /// https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%.next, captured into
    /// <see cref="IteratorPrototype"/>'s original <c>next</c> so that an untouched segment iterator steps natively, as
    /// every other built-in kind does. Step 2's RequireInternalSlot is the check every built-in iterator prototype's
    /// <c>next</c> makes here, that the receiver is a built-in iterator, not that it is a segment iterator:
    /// <see cref="IteratorInstance.TryGetIterator"/> steps an iterator natively whichever built-in prototype it
    /// inherits, which is the same answer only while no built-in <c>next</c> checks the kind.
    /// </summary>
    [JsFunction(Name = "next", CaptureField = nameof(_originalNextFunction))]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);
}
