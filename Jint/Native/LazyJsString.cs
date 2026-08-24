using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// A JavaScript string whose text is produced on demand: the base class answers everything it can from
/// the length alone, and asks the host for the characters only when something genuinely needs them.
/// </summary>
/// <remarks>
/// <para>
/// Use it when the text is expensive to produce and a script may never ask for it — a database blob, a
/// projected document field, an encoded buffer, a value behind a native handle. A subclass supplies the
/// length up front and one method:
/// </para>
/// <code>
/// sealed class DocumentField : LazyJsString
/// {
///     private readonly byte[] _utf8;
///
///     public DocumentField(byte[] utf8, int length) : base(length) => _utf8 = utf8;
///
///     protected override string Materialize() => Encoding.UTF8.GetString(_utf8);
/// }
/// </code>
/// <para>
/// <b><see cref="Materialize"/> runs at most once.</b> This class memoizes what it returns, so a subclass
/// needs no caching field of its own, and <see cref="ToString()"/> is sealed so that the memoization cannot
/// be bypassed. It must return the string this value stands for and never <see langword="null"/>; returning
/// <see langword="null"/> throws an <see cref="InvalidOperationException"/> naming the type, rather than
/// letting a null reach the engine and surface as a <see cref="NullReferenceException"/> somewhere else
/// entirely. It must also not call back into this instance — <see cref="ToString()"/>, an equality
/// comparison, anything that needs the text — because the memo is not set until it returns.
/// </para>
/// <para>
/// <b>Thread safety: none, deliberately.</b> The memo is a plain field written without synchronization,
/// which is the right trade because an <see cref="Engine"/> rejects concurrent use, so every read the
/// engine performs is on one thread. A host that reads the same instance from several of its own threads
/// may see <see cref="Materialize"/> run more than once and different callers receive different (but
/// equal) <see cref="string"/> instances; nothing tears, because the field is only ever written with a
/// complete value. Keep <see cref="Materialize"/> pure, and the worst outcome is wasted work.
/// </para>
/// <para>
/// <b><see cref="Length"/> is the whole point.</b> It is answered from the value handed to the
/// constructor, so it never materializes — and neither do the engine paths that need only a length:
/// <c>str.length</c>, truthiness, and the length comparison <see cref="JsString.Equals(JsString)"/>
/// performs first, which is what lets a comparison against a string of a different length stay free. The
/// length is fixed at construction because a JavaScript string is immutable; if the text you eventually
/// produce is not that long, every one of those shortcuts is wrong, which is why
/// <a href="https://github.com/sebastienros/jint#embedding-performance">host-contract verification</a>
/// checks the two against each other at materialization time.
/// </para>
/// <para>
/// <b>What is still worth overriding.</b> <see cref="this[int]"/> defaults to indexing the materialized
/// text; a store that can answer a single character without decoding the whole value — an ASCII or
/// fixed-width buffer, a view over a larger string — should override it, and then character access joins
/// the list of things that never materialize. <see cref="Length"/> stays virtual for the same reason.
/// Everything else that needs the text — <c>JSON.stringify</c>, use as a property key, a same-length
/// content comparison, any <c>String.prototype</c> method — has to have it, and materializing once is
/// exactly what this class is for.
/// </para>
/// </remarks>
public abstract class LazyJsString : JsString
{
    private readonly int _length;

    /// <summary>
    /// Creates a lazy string of a known length.
    /// </summary>
    /// <param name="length">
    /// The number of UTF-16 code units <see cref="Materialize"/> will produce. It is answered to script
    /// without materializing anything, so it has to be right.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, or longer than a JavaScript string may be.
    /// </exception>
    protected LazyJsString(int length) : base(null!)
    {
        if (length < 0)
        {
            Throw.ArgumentOutOfRangeException(nameof(length), "A string cannot have a negative length.");
        }

        if (length > MaxLength)
        {
            Throw.ArgumentOutOfRangeException(nameof(length), $"A JavaScript string cannot hold more than {MaxLength} code units.");
        }

        _length = length;
    }

    /// <summary>
    /// Produces the text this value stands for. Called at most once per instance, on the first read that
    /// actually needs the characters; the result is memoized by this class.
    /// </summary>
    /// <returns>
    /// The string, of exactly <see cref="Length"/> code units. Never <see langword="null"/>.
    /// </returns>
    protected abstract string Materialize();

    /// <summary>
    /// The length supplied to the constructor. Answered without materializing.
    /// </summary>
    public override int Length => _length;

    /// <summary>
    /// The character at <paramref name="index"/>, read out of the materialized text. Override this when
    /// the backing store can answer a single character more cheaply than it can produce the whole value.
    /// </summary>
    public override char this[int index] => ToString()[index];

    /// <summary>
    /// The text, materializing it on the first call and returning the memoized value afterwards. Sealed:
    /// a subclass supplies <see cref="Materialize"/> instead, so that "at most once" is a property of this
    /// class rather than something every host has to re-implement.
    /// </summary>
    public sealed override string ToString() => _value ?? MaterializeAndMemoize();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string MaterializeAndMemoize()
    {
        var value = Materialize();
        if (value is null)
        {
            Throw.InvalidOperationException($"{GetType()}.Materialize() returned null. It must return the text this string stands for.");
        }

        // Memoized before anything reads back through this instance: the length check below dispatches to
        // Length, which a subclass may have overridden with something that reaches for the text, and it
        // has to find the memo rather than re-enter Materialize().
        _value = value;

        if (HostContractVerification.Enabled)
        {
            // No out-of-assembly gate: nothing in Jint derives from this class - its own lazy strings
            // (SlicedString, ConcatenatedString) predate it and answer their lengths from their own
            // fields - so every instance reaching here is a host type by construction. The check is one
            // integer comparison on the path that just did the expensive part, so there is nothing to
            // save by narrowing it further.
            VerifyDeclaredLength(value);
        }

        return value;
    }

    /// <summary>
    /// The obligation a lazy string takes on in exchange for being asked nothing until it is read: the
    /// length it declared has to be the length it produces. The engine trusts the declared length on every
    /// path that can answer from it alone, so a mismatch is silent and consequential — a string equal to
    /// <c>'abc'</c> compares unequal to it, and an index the host considers in range reads past the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void VerifyDeclaredLength(string value)
    {
        var declared = Length;
        if (declared != value.Length)
        {
            HostContractVerification.Fail(
                $"{GetType()} declared a length of {declared} but Materialize() produced {value.Length} code units ('{value}'). "
                + "The declared length is what str.length, truthiness and the length comparison in string equality answer from, so the two have to agree.");
        }
    }
}
