using System.Collections;
using AngleSharp;
using AngleSharp.Dom;

namespace Jint.Browser.Observers;

/// <summary>
/// The record a callback is handed: AngleSharp's, with the two node lists made non-null.
/// </summary>
/// <remarks>
/// <para>
/// <a href="https://dom.spec.whatwg.org/#interface-mutationrecord">DOM §4.3.3</a> declares
/// <c>addedNodes</c> and <c>removedNodes</c> as <c>[SameObject] readonly attribute NodeList</c> — never
/// nullable — so a page may write <c>record.addedNodes.length</c> for a record of any type.
/// <c>MutationRecord.Attributes</c> and <c>MutationRecord.CharacterData</c> leave AngleSharp's own
/// <c>Added</c> and <c>Removed</c> <see langword="null"/>, which the binding would project as
/// <see langword="null"/>; this decorator answers an empty list instead.
/// </para>
/// <para>
/// It is a decorator rather than a patch to the generated <c>MutationRecord</c> binding because the fix
/// belongs to the record AngleSharp made, not to the projection: everything else — the type string, the
/// target, the siblings, and <c>oldValue</c> already cleared by <c>MutationRecord.Copy</c> when the observer
/// did not ask for it — is right, and <c>DomTypeMap</c> matches this on <see cref="IMutationRecord"/> exactly
/// as it matches AngleSharp's own, so <c>record instanceof MutationRecord</c> still holds.
/// </para>
/// </remarks>
internal sealed class DeliveredMutationRecord : IMutationRecord
{
    private readonly IMutationRecord _record;

    internal DeliveredMutationRecord(IMutationRecord record)
    {
        _record = record;
    }

    /// <inheritdoc />
    public string Type => _record.Type;

    /// <inheritdoc />
    public INode Target => _record.Target;

    /// <inheritdoc />
    public INodeList Added => _record.Added ?? EmptyNodeList.Instance;

    /// <inheritdoc />
    public INodeList Removed => _record.Removed ?? EmptyNodeList.Instance;

    /// <inheritdoc />
    public INode PreviousSibling => _record.PreviousSibling!;

    /// <inheritdoc />
    public INode NextSibling => _record.NextSibling!;

    /// <inheritdoc />
    public string AttributeName => _record.AttributeName!;

    /// <inheritdoc />
    public string AttributeNamespace => _record.AttributeNamespace!;

    /// <inheritdoc />
    public string PreviousValue => _record.PreviousValue!;
}

/// <summary>
/// The <c>NodeList</c> an attribute or character-data record answers for its added and removed nodes.
/// </summary>
/// <remarks>
/// One process-shared instance, because it holds nothing. That means every such record shares one wrapper in
/// an engine's wrapper cache, so <c>a.addedNodes === b.addedNodes</c> answers <see langword="true"/> across
/// two empty records where a browser answers <see langword="false"/>. Nothing a page does with an empty list
/// can tell the difference except that identity comparison, and the alternative — one allocation per record
/// per engine, kept alive by the cache — costs more than the divergence is worth.
/// </remarks>
internal sealed class EmptyNodeList : INodeList
{
    internal static readonly EmptyNodeList Instance = new();

    private EmptyNodeList()
    {
    }

    /// <inheritdoc />
    public INode this[int index] => throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc />
    public int Length => 0;

    /// <inheritdoc />
    public IEnumerator<INode> GetEnumerator()
    {
        yield break;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void ToHtml(TextWriter writer, IMarkupFormatter formatter)
    {
    }
}
