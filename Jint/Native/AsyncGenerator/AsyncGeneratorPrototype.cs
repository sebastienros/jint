using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-object
/// </summary>
[JsObject]
internal sealed partial class AsyncGeneratorPrototype : ObjectInstance
{
    private readonly Realm _realm;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.Configurable)]
    private readonly AsyncGeneratorFunctionPrototype _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString AsyncGeneratorToStringTag = new("AsyncGenerator");

    internal AsyncGeneratorPrototype(
        Engine engine,
        Realm realm,
        AsyncGeneratorFunctionPrototype constructor,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine)
    {
        _realm = realm;
        _constructor = constructor;
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-next
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Next(JsValue thisObject, JsValue value)
    {
        // Per spec: If generator is not valid, return a rejected promise
        if (!TryGetAsyncGeneratorInstance(thisObject, out var g))
        {
            return CreateRejectedPromiseWithTypeError("object must be an AsyncGenerator instance");
        }

        var completion = new Completion(CompletionType.Normal, value, null!);
        return g.AsyncGeneratorEnqueue(completion);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-return
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Return(JsValue thisObject, JsValue value)
    {
        // Per spec: If generator is not valid, return a rejected promise
        if (!TryGetAsyncGeneratorInstance(thisObject, out var g))
        {
            return CreateRejectedPromiseWithTypeError("object must be an AsyncGenerator instance");
        }

        var completion = new Completion(CompletionType.Return, value, null!);
        return g.AsyncGeneratorEnqueue(completion);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-throw
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Throw(JsValue thisObject, JsValue exception)
    {
        // Per spec: If generator is not valid, return a rejected promise
        if (!TryGetAsyncGeneratorInstance(thisObject, out var g))
        {
            return CreateRejectedPromiseWithTypeError("object must be an AsyncGenerator instance");
        }

        var completion = new Completion(CompletionType.Throw, exception, null!);
        return g.AsyncGeneratorEnqueue(completion);
    }

    private static bool TryGetAsyncGeneratorInstance(JsValue thisObj, out AsyncGeneratorInstance instance)
    {
        instance = (thisObj as AsyncGeneratorInstance)!;
        return instance is not null;
    }

    /// <summary>
    /// Creates a rejected promise with a TypeError.
    /// Per spec, when the generator is invalid, we return a rejected promise instead of throwing.
    /// </summary>
    private JsValue CreateRejectedPromiseWithTypeError(string message)
    {
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        var error = _realm.Intrinsics.TypeError.Construct(message);
        promiseCapability.Reject.Call(Undefined, new[] { error });
        return promiseCapability.PromiseInstance;
    }
}
