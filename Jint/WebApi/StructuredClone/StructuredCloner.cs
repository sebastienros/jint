#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.StructuredClone;

/// <summary>
/// StructuredSerializeWithTransfer followed by StructuredDeserializeWithTransfer in one realm — which is all
/// <c>structuredClone</c> is.
/// <para>
/// https://html.spec.whatwg.org/multipage/structured-data.html#dom-structuredclone
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The two phases are run as the specification writes them, through the engine-neutral
/// <see cref="SerializationRecord"/> in between, rather than being fused into a single walk that builds the
/// clone directly. The fused form was cheaper — it allocated no intermediate graph — but it meant Jint had
/// <i>two</i> structured-clone algorithms once <c>MessagePort</c> needed the real two-phase one, and nothing
/// would have made the pair stay in agreement. Composing instead makes the whole of
/// <c>structuredClone</c>'s behaviour a test of the serializer and the deserializer that channel messaging
/// uses, which is the property worth paying an intermediate graph for: this is an opt-in host API, not
/// anything on the interpreter's hot path.
/// </para>
/// <para>
/// Nothing observable moved with it. The serializer's walk is the fused walk, so getters run in the same order
/// on the same values; a throw part-way through still discards a half-built result nothing can reach; and the
/// transfer steps still run after the whole walk, which is what lets a getter reached during the walk resize
/// or write into a buffer the caller is transferring and have the clone see the result.
/// </para>
/// </remarks>
internal static class StructuredCloner
{
    /// <summary>
    /// Clones <paramref name="value"/> into <paramref name="realm"/>.
    /// </summary>
    /// <param name="engine">The engine that owns both the source graph and the clone.</param>
    /// <param name="realm">The realm whose intrinsic prototypes the clone is built with.</param>
    /// <param name="value">The value to clone.</param>
    /// <param name="transferList">
    /// The already-iterated <c>transfer</c> option, or <see langword="null"/> when the caller passed none.
    /// </param>
    internal static JsValue Clone(Engine engine, Realm realm, JsValue value, List<JsValue>? transferList)
    {
        var record = new StructuredSerializer(engine, realm).Serialize(value, transferList);
        return new StructuredDeserializer(engine, realm).Deserialize(in record);
    }
}
#endif
