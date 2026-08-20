#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.StructuredClone;

/// <summary>
/// The global <c>structuredClone(value, options)</c> function.
/// <para>
/// https://html.spec.whatwg.org/multipage/structured-data.html#dom-structuredclone
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Three steps, all of them delegated: convert the options dictionary, run
/// StructuredSerializeWithTransfer, run StructuredDeserializeWithTransfer in this function's own realm. The
/// last two are <see cref="StructuredCloner"/>, which runs them as two genuine phases through an
/// engine-neutral serialization record — the same pair <c>MessagePort</c> uses to move a value between two
/// engines.
/// </para>
/// <para>
/// A fresh serializer and deserializer per call is the specification's "let memory be an empty map": two
/// calls share no identity, so <c>structuredClone(x) !== structuredClone(x)</c>, and a getter that re-enters
/// <c>structuredClone</c> during a clone gets its own memory rather than joining the outer one.
/// </para>
/// </remarks>
internal sealed class StructuredCloneFunction : Jint.Native.Function.Function
{
    private static readonly JsString _functionName = new("structuredClone");

    internal StructuredCloneFunction(Engine engine, Realm realm, FunctionPrototype functionPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        // `any value` is a required WebIDL argument, so calling with none is a TypeError rather than a clone
        // of undefined. https://webidl.spec.whatwg.org/#dfn-create-operation-function
        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to execute 'structuredClone': 1 argument required, but only 0 present.");
        }

        var transferList = StructuredSerializeOptions.ReadTransferOption(_realm, arguments.At(1), "structuredClone");
        return StructuredCloner.Clone(_engine, _realm, arguments[0], transferList);
    }
}
#endif
