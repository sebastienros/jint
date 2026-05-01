using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.String;

/// <summary>
/// https://tc39.es/ecma262/#sec-%stringiteratorprototype%-object
/// </summary>
[JsObject]
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

    [JsFunction(Length = 0, Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    public ObjectInstance Construct(string str)
    {
        var instance = new IteratorInstance.StringIterator(Engine, str)
        {
            _prototype = this
        };

        return instance;
    }
}
