#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.StructuredClone;

/// <summary>
/// The <c>StructuredSerializeOptions</c> dictionary — one member, <c>sequence&lt;object&gt; transfer = []</c>.
/// <para>
/// https://html.spec.whatwg.org/multipage/structured-data.html#dictdef-structuredserializeoptions
/// </para>
/// </summary>
/// <remarks>
/// Shared by <c>structuredClone(value, options)</c> and <c>MessagePort.postMessage(message, options)</c>,
/// which take the same dictionary. Reading it runs before any serialization, so a malformed <c>transfer</c>
/// option is a <c>TypeError</c> and nothing has been walked, let alone detached.
/// </remarks>
internal static class StructuredSerializeOptions
{
    private static readonly JsString _transfer = new("transfer");

    /// <summary>
    /// Reads the <c>transfer</c> member of an options dictionary.
    /// </summary>
    /// <param name="realm">The realm whose <c>TypeError</c> is raised for a malformed argument.</param>
    /// <param name="options">The dictionary argument, which may be absent, <see langword="null"/> or an object.</param>
    /// <param name="operation">
    /// How the operation names itself in an error message, e.g. <c>"structuredClone"</c> or
    /// <c>"postMessage' on 'MessagePort"</c>.
    /// </param>
    internal static List<JsValue>? ReadTransferOption(Realm realm, JsValue options, string operation)
    {
        // An omitted or null dictionary is the empty dictionary.
        if (options.IsNullOrUndefined())
        {
            return null;
        }

        if (options is not ObjectInstance optionsObject)
        {
            Throw.TypeError(realm, $"Failed to execute '{operation}': The provided value is not of type 'StructuredSerializeOptions'.");
            return null;
        }

        var transfer = optionsObject.Get(_transfer);
        if (transfer.IsUndefined())
        {
            return null;
        }

        return ReadTransferSequence(realm, transfer, operation);
    }

    /// <summary>
    /// The <c>sequence&lt;object&gt;</c> conversion, https://webidl.spec.whatwg.org/#es-sequence: the argument
    /// is iterated through the iterator protocol, not through <c>length</c>, so any iterable will do; each
    /// element must be an object, which is all WebIDL checks here — whether it is <i>transferable</i> is a
    /// <c>DataCloneError</c> decided later, in the order the serialization algorithm specifies.
    /// </summary>
    internal static List<JsValue> ReadTransferSequence(Realm realm, JsValue transfer, string operation)
    {
        var iterator = transfer.GetIterator(realm);
        var list = new List<JsValue>();
        while (iterator.TryIteratorStepValue(out var item))
        {
            if (item is not ObjectInstance)
            {
                iterator.Close(CompletionType.Throw);
                Throw.TypeError(realm, $"Failed to execute '{operation}': The provided value is not of type 'object'.");
            }

            list.Add(item);
        }

        return list;
    }
}
#endif
