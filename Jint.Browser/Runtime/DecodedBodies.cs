using System.Runtime.CompilerServices;

namespace Jint.Browser.Runtime;

/// <summary>
/// The text a response body decodes to, kept with the body's array for as long as the array lives.
/// </summary>
/// <remarks>
/// A body the HTTP cache serves is the cache's own array (see <c>HttpResponseCache.StoredBodyContent</c>), so a
/// context's pages decoding the same script or style sheet decode the same array: the first decodes it, the next
/// ones share its text, which is immutable. A body read from the network is an array of its own and is decoded
/// once anyway. The pages of a context run on loops of their own, so the slot is locked.
/// </remarks>
internal static class DecodedBodies
{
    private static readonly ConditionalWeakTable<byte[], Slot> Slots = new();

    private sealed class Slot
    {
        // One encoding per body is the rule: a script and a style sheet are each decoded the one way their
        // response and their document decide, and the rare body decoded two ways keeps the last.
        internal string? Key;
        internal (string Text, string Encoding) Result;
    }

    /// <summary>
    /// The text of <paramref name="bytes"/> and the encoding it was decoded with, from <paramref name="decode"/> the
    /// first time this array is decoded under <paramref name="key"/>.
    /// </summary>
    /// <param name="bytes">The body, which nothing changes once it is fetched.</param>
    /// <param name="key">What decides the decoding besides the bytes: the encoding, or the labels it is chosen from.</param>
    /// <param name="state">What <paramref name="decode"/> needs besides the bytes.</param>
    /// <param name="decode">Decodes the bytes; it must depend on nothing but its arguments.</param>
    internal static (string Text, string Encoding) GetOrDecode<TState>(byte[] bytes, string key, TState state,
        Func<byte[], TState, (string Text, string Encoding)> decode)
    {
        var slot = Slots.GetValue(bytes, static _ => new Slot());
        lock (slot)
        {
            if (string.Equals(slot.Key, key, StringComparison.Ordinal))
            {
                return slot.Result;
            }
        }

        var result = decode(bytes, state);
        lock (slot)
        {
            slot.Key = key;
            slot.Result = result;
        }

        return result;
    }
}
