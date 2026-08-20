#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
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
/// last two are fused into <see cref="StructuredCloner"/>, which documents why that is unobservable here.
/// </para>
/// <para>
/// A fresh <see cref="StructuredCloner"/> per call is the specification's "let memory be an empty map": two
/// calls share no identity, so <c>structuredClone(x) !== structuredClone(x)</c>, and a getter that re-enters
/// <c>structuredClone</c> during a clone gets its own memory rather than joining the outer one.
/// </para>
/// </remarks>
internal sealed class StructuredCloneFunction : Jint.Native.Function.Function
{
    private static readonly JsString _functionName = new("structuredClone");
    private static readonly JsString _transfer = new("transfer");

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

        var transferList = ReadTransferList(arguments.At(1));
        return new StructuredCloner(_engine, _realm).Clone(arguments[0], transferList);
    }

    /// <summary>
    /// The <c>StructuredSerializeOptions</c> dictionary — one member, <c>sequence&lt;object&gt; transfer = []</c>.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/structured-data.html#dictdef-structuredserializeoptions
    /// </para>
    /// </summary>
    /// <remarks>
    /// This runs before any cloning, so a malformed <c>transfer</c> option is a <c>TypeError</c> and nothing
    /// has been walked, let alone detached. A <c>sequence</c> is converted through the iterator protocol, not
    /// through <c>length</c>, so any iterable will do; each element must be an object, which is all WebIDL
    /// checks here — whether it is <i>transferable</i> is a DataCloneError decided later, in the order the
    /// serialization algorithm specifies.
    /// </remarks>
    private List<JsValue>? ReadTransferList(JsValue options)
    {
        // An omitted or null dictionary is the empty dictionary.
        if (options.IsNullOrUndefined())
        {
            return null;
        }

        if (options is not ObjectInstance optionsObject)
        {
            Throw.TypeError(_realm, "Failed to execute 'structuredClone': The provided value is not of type 'StructuredSerializeOptions'.");
            return null;
        }

        var transfer = optionsObject.Get(_transfer);
        if (transfer.IsUndefined())
        {
            return null;
        }

        var iterator = transfer.GetIterator(_realm);
        var list = new List<JsValue>();
        while (iterator.TryIteratorStepValue(out var item))
        {
            if (item is not ObjectInstance)
            {
                iterator.Close(CompletionType.Throw);
                Throw.TypeError(_realm, "Failed to execute 'structuredClone': The provided value is not of type 'object'.");
            }

            list.Add(item);
        }

        return list;
    }
}
#endif
