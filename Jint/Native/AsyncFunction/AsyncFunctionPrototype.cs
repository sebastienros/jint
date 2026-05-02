using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AsyncFunction;

/// <summary>
/// https://tc39.es/ecma262/#sec-async-function-prototype-properties
/// </summary>
[JsObject]
internal sealed partial class AsyncFunctionPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly AsyncFunctionConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ToStringTagValue = new("AsyncFunction");

    public AsyncFunctionPrototype(
        Engine engine,
        Realm realm,
        AsyncFunctionConstructor constructor,
        FunctionPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }
}
