using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.String;

/// <summary>
/// https://tc39.es/ecma262/#sec-%stringiteratorprototype%-object
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class StringIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString StringIteratorToStringTag = new("String Iterator");

    internal StringIteratorPrototype(
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

    [JsFunction(Name = "next", CaptureField = nameof(_originalNextFunction))]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    public ObjectInstance Construct(string str)
    {
        return new IteratorInstance.StringIterator(Engine, str, this);
    }

    /// <summary>
    /// The iterator record GetIterator builds for a string primitive whose <c>@@iterator</c> is
    /// <c>String.prototype</c>'s own (https://tc39.es/ecma262/#sec-string.prototype-%symbol.iterator%): the method
    /// answers a fresh CreateStringIterator object (https://tc39.es/ecma262/#sec-createstringiterator), and
    /// GetIteratorFromMethod step 3 reads <c>next</c> off it. The fresh object has no own property, so that read
    /// finds this prototype's: while it is the built-in <c>next</c> the record is a native string iterator nothing
    /// can see; otherwise it is the specified object, wrapped so that its one read and every step go through the
    /// replacement (sebastienros/jint#4405).
    /// </summary>
    internal IteratorInstance ToIteratorRecord(string str)
    {
        // Every write to an own property initializes the prototype first, so an uninitialized one still holds the
        // built-in `next` - and the native lane does not have to initialize it to find that out.
        return !_initialized || HasOriginalNext()
            ? new IteratorInstance.StringIterator(_engine, str, this)
            : IteratorInstance.WrapReadingNext(Construct(str));
    }
}
