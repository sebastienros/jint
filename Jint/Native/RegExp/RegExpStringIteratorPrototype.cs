using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.RegExp;

/// <summary>
/// https://tc39.es/ecma262/#sec-%regexpstringiteratorprototype%-object
/// </summary>
[JsObject]
internal sealed partial class RegExpStringIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString RegExpStringIteratorToStringTag = new("RegExp String Iterator");

    internal RegExpStringIteratorPrototype(
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

    [JsFunction(Length = 0, Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    internal IteratorInstance Construct(ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode)
    {
        var instance = new IteratorInstance.RegExpStringIterator(Engine, iteratingRegExp, iteratedString, global, unicode)
        {
            _prototype = this
        };

        return instance;
    }
}
