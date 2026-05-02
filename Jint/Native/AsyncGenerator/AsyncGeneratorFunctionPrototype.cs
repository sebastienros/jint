using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-asyncgeneratorfunction-prototype
/// </summary>
[JsObject]
internal sealed partial class AsyncGeneratorFunctionPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.Configurable)]
    private readonly AsyncGeneratorFunctionConstructor _constructor;

    [JsProperty(Name = "prototype", Flags = PropertyFlag.Configurable)]
    private readonly AsyncGeneratorPrototype _prototypeObject;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ToStringTagValue = new("AsyncGeneratorFunction");

    internal AsyncGeneratorFunctionPrototype(
        Engine engine,
        AsyncGeneratorFunctionConstructor constructor,
        FunctionPrototype prototype,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine, engine.Realm)
    {
        _constructor = constructor;
        _prototype = prototype;
        _prototypeObject = new AsyncGeneratorPrototype(engine, engine.Realm, this, asyncIteratorPrototype);
    }

    public AsyncGeneratorPrototype PrototypeObject => _prototypeObject;

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }
}
