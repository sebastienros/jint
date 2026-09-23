using System.Xml;

namespace Jint.HtmlParser;

internal enum TextAppendCheckpoint
{
    DuringPreparation,
    AfterPreparation,
    AfterCommit
}

/// <summary>A text node.</summary>
public sealed class Text : Node
{
    private string _data = string.Empty;
    private char[]? _parsedStorage;
    private int _parsedLength;
    private string? _cachedParsedData;

    internal Text(Document owner, string data) : base(owner) => _data = data ?? throw new ArgumentNullException(nameof(data));
    public override NodeType NodeType => NodeType.Text;
    public string Data
    {
        get => _parsedStorage is null ? _data : _cachedParsedData ??= new string(_parsedStorage, 0, _parsedLength);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
            var oldValue = matches?.NeedsOldValue == true ? Data : null;
            _data = value;
            _parsedStorage = null;
            _parsedLength = 0;
            _cachedParsedData = null;
            OwnerDocument!.MarkMutation();
            MutationTracking.QueueCharacterData(this, oldValue, matches);
        }
    }

    internal void AppendParsedData(ReadOnlySpan<char> data, CancellationToken cancellationToken)
        => AppendParsedData(data, null, cancellationToken);

    // A parser slice is prepared beyond the published length. Cancellation before
    // the commit keeps Data unchanged; after it, the whole slice is observable.
    internal void AppendParsedData(ReadOnlySpan<char> data, Action<TextAppendCheckpoint>? workCheckpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (data.IsEmpty)
        {
            return;
        }

        var oldLength = _parsedStorage is null ? _data.Length : _parsedLength;
        var newLength = checked(oldLength + data.Length);
        var storage = _parsedStorage;
        if (storage is null || storage.Length < newLength)
        {
            var currentCapacity = storage?.Length ?? 0;
            var doubledCapacity = currentCapacity > Array.MaxLength / 2 ? Array.MaxLength : Math.Max(16, currentCapacity * 2);
            storage = new char[Math.Max(newLength, doubledCapacity)];
            cancellationToken.ThrowIfCancellationRequested();
            if (_parsedStorage is { } previous)
            {
                previous.AsSpan(0, oldLength).CopyTo(storage);
            }
            else
            {
                _data.AsSpan().CopyTo(storage);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        const int CopySlice = 4096;
        for (var offset = 0; offset < data.Length; offset += CopySlice)
        {
            workCheckpoint?.Invoke(TextAppendCheckpoint.DuringPreparation);
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(CopySlice, data.Length - offset);
            data.Slice(offset, count).CopyTo(storage.AsSpan(oldLength + offset, count));
        }

        workCheckpoint?.Invoke(TextAppendCheckpoint.AfterPreparation);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
        var oldValue = matches?.NeedsOldValue == true ? Data : null;

        _parsedStorage = storage;
        _parsedLength = newLength;
        _data = string.Empty;
        _cachedParsedData = null;
        OwnerDocument!.MarkMutation();
        MutationTracking.QueueCharacterData(this, oldValue, matches);

        workCheckpoint?.Invoke(TextAppendCheckpoint.AfterCommit);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>A comment node.</summary>
public sealed class Comment : Node
{
    private string _data = string.Empty;

    internal Comment(Document owner, string data) : base(owner) => _data = data ?? throw new ArgumentNullException(nameof(data));
    public override NodeType NodeType => NodeType.Comment;
    public string Data
    {
        get => _data;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
            var oldValue = matches?.NeedsOldValue == true ? _data : null;
            _data = value;
            OwnerDocument!.MarkMutation();
            MutationTracking.QueueCharacterData(this, oldValue, matches);
        }
    }
}

/// <summary>An XML CDATA section.</summary>
public sealed class CDataSection : Node
{
    private string _data = string.Empty;

    internal CDataSection(Document owner, string data, bool clone = false) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!clone && data.Contains("]]>", StringComparison.Ordinal)) throw DomException.InvalidCharacter();
        _data = data;
    }
    public override NodeType NodeType => NodeType.CDataSection;
    public string Data
    {
        get => _data;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
            var oldValue = matches?.NeedsOldValue == true ? _data : null;
            _data = value;
            OwnerDocument!.MarkMutation();
            MutationTracking.QueueCharacterData(this, oldValue, matches);
        }
    }
}

/// <summary>An XML processing instruction.</summary>
public sealed class ProcessingInstruction : Node
{
    private string _data = string.Empty;

    internal ProcessingInstruction(Document owner, string target, string data, bool clone = false) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(target);
        // DOM Standard §4.13 uses XML's Name production for PI targets.
        try { Target = XmlConvert.VerifyName(target); }
        catch (XmlException) { throw DomException.InvalidCharacter(); }

        ArgumentNullException.ThrowIfNull(data);
        if (!clone && data.Contains("?>", StringComparison.Ordinal)) throw DomException.InvalidCharacter();
        _data = data;
    }

    public override NodeType NodeType => NodeType.ProcessingInstruction;
    public string Target { get; }
    public string Data
    {
        get => _data;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
            var oldValue = matches?.NeedsOldValue == true ? _data : null;
            _data = value;
            OwnerDocument!.MarkMutation();
            MutationTracking.QueueCharacterData(this, oldValue, matches);
        }
    }
}

/// <summary>A document type declaration.</summary>
public sealed class DocumentType : Node
{
    internal DocumentType(Document owner, string name, string publicId, string systemId) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(name);
        // DOM Standard §1.4 permits an empty doctype name, but excludes ASCII
        // whitespace, NUL and '>'. It is intentionally not an XML Name check.
        foreach (var character in name)
        {
            if (character is '\t' or '\n' or '\f' or '\r' or ' ' or '\0' or '>')
            {
                throw DomException.InvalidCharacter();
            }
        }

        Name = name;
        PublicId = publicId ?? throw new ArgumentNullException(nameof(publicId));
        SystemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
    }

    public override NodeType NodeType => NodeType.DocumentType;
    public string Name { get; }
    public string PublicId { get; }
    public string SystemId { get; }
}

/// <summary>A detached container whose children can be inserted as a group.</summary>
public sealed class DocumentFragment : Node
{
    internal DocumentFragment(Document owner, Element? host = null) : base(owner) => Host = host;
    public override NodeType NodeType => NodeType.DocumentFragment;
    internal Element? Host { get; }
}
