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
    internal int DataLength => _parsedStorage is null ? _data.Length : _parsedLength;
    internal char DataAt(int index) => _parsedStorage is null ? _data[index] : _parsedStorage[index];
    public string Data
    {
        get => _parsedStorage is null ? _data : _cachedParsedData ??= new string(_parsedStorage, 0, _parsedLength);
        set => ReplaceDataCore(value, 0, BoundaryOrder.GetLength(new DomNodeIdentity(this)), (uint) (value?.Length ?? 0));
    }

    internal void ReplaceDataCore(string value, uint offset, uint count, uint insertedLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
        var oldValue = matches?.NeedsOldValue == true ? Data : null;
        LiveTraversalTracking.ReplaceData(this, offset, count, insertedLength);
        _data = value;
        _parsedStorage = null;
        _parsedLength = 0;
        _cachedParsedData = null;
        OwnerDocument!.MarkMutation();
        if (ParentNode is { } parent) HtmlTextAreaMutations.ChildrenChanged(parent);
        MutationTracking.QueueCharacterData(this, oldValue, matches);
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

        LiveTraversalTracking.ReplaceData(this, (uint) oldLength, 0, (uint) data.Length);
        _parsedStorage = storage;
        _parsedLength = newLength;
        _data = string.Empty;
        _cachedParsedData = null;
        OwnerDocument!.MarkMutation();
        if (ParentNode is { } parent) HtmlTextAreaMutations.ChildrenChanged(parent, mayShorten: false);
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
        set => ReplaceDataCore(value, 0, BoundaryOrder.GetLength(new DomNodeIdentity(this)), (uint) (value?.Length ?? 0));
    }

    internal void ReplaceDataCore(string value, uint offset, uint count, uint insertedLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
        var oldValue = matches?.NeedsOldValue == true ? _data : null;
        LiveTraversalTracking.ReplaceData(this, offset, count, insertedLength);
        _data = value;
        OwnerDocument!.MarkMutation();
        if (ParentNode is { } parent) HtmlTextAreaMutations.ChildrenChanged(parent);
        MutationTracking.QueueCharacterData(this, oldValue, matches);
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
        set => ReplaceDataCore(value, 0, BoundaryOrder.GetLength(new DomNodeIdentity(this)), (uint) (value?.Length ?? 0));
    }

    internal void ReplaceDataCore(string value, uint offset, uint count, uint insertedLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
        var oldValue = matches?.NeedsOldValue == true ? _data : null;
        LiveTraversalTracking.ReplaceData(this, offset, count, insertedLength);
        _data = value;
        OwnerDocument!.MarkMutation();
        if (ParentNode is { } parent) HtmlTextAreaMutations.ChildrenChanged(parent);
        MutationTracking.QueueCharacterData(this, oldValue, matches);
    }
}

/// <summary>An XML processing instruction.</summary>
public sealed class ProcessingInstruction : Node
{
    private string _data = string.Empty;

    internal ProcessingInstruction(Document owner, string target, string data)
        : this(owner, ValidatePublicState(target, data)) { }

    private ProcessingInstruction(Document owner, InitialState state) : base(owner)
    {
        Target = state.Target;
        _data = state.Data;
    }

    // The XML parser has already proved Name, namespace restrictions, delimiters,
    // character legality, and bounds. Copying existing state has a distinct proof:
    // Data can have been edited after creation to contain "?>".
    internal static ProcessingInstruction FromParsed(Document owner, string target, string data)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(data);
        return new ProcessingInstruction(owner, new InitialState(target, data));
    }

    internal static ProcessingInstruction CopyTo(Document owner, ProcessingInstruction source)
        => new(owner, new InitialState(source.Target, source.Data));

    private static InitialState ValidatePublicState(string target, string data)
    {
        ArgumentNullException.ThrowIfNull(target);
        // DOM Standard §4.13 uses XML 1.0 fifth edition Name, not QName/NCName.
        // https://dom.spec.whatwg.org/#interface-processinginstruction
        // https://www.w3.org/TR/xml/#NT-Name
        if (!IsXmlName(target))
        {
            throw DomException.InvalidCharacter();
        }

        ArgumentNullException.ThrowIfNull(data);
        if (data.Contains("?>", StringComparison.Ordinal))
        {
            throw DomException.InvalidCharacter();
        }

        return new InitialState(target, data);
    }

    private static bool IsXmlName(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var index = 0;
        if (!TryReadScalar(value, ref index, out var scalar) || !IsNameStart(scalar))
        {
            return false;
        }

        while (index < value.Length)
        {
            if (!TryReadScalar(value, ref index, out scalar) ||
                !IsNameStart(scalar) && scalar is not ('-' or '.' or >= '0' and <= '9' or 0xB7 or
                    >= 0x0300 and <= 0x036F or >= 0x203F and <= 0x2040))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadScalar(string value, ref int index, out int scalar)
    {
        var first = value[index++];
        if (!char.IsSurrogate(first))
        {
            scalar = first;
            return true;
        }

        if (!char.IsHighSurrogate(first) || index == value.Length || !char.IsLowSurrogate(value[index]))
        {
            scalar = 0;
            return false;
        }

        scalar = char.ConvertToUtf32(first, value[index++]);
        return true;
    }

    private static bool IsNameStart(int scalar)
        => scalar is ':' or '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
            >= 0xC0 and <= 0xD6 or >= 0xD8 and <= 0xF6 or >= 0xF8 and <= 0x2FF or
            >= 0x370 and <= 0x37D or >= 0x37F and <= 0x1FFF or >= 0x200C and <= 0x200D or
            >= 0x2070 and <= 0x218F or >= 0x2C00 and <= 0x2FEF or >= 0x3001 and <= 0xD7FF or
            >= 0xF900 and <= 0xFDCF or >= 0xFDF0 and <= 0xFFFD or >= 0x10000 and <= 0xEFFFF;

    private readonly struct InitialState(string target, string data)
    {
        internal string Target { get; } = target;
        internal string Data { get; } = data;
    }

    public override NodeType NodeType => NodeType.ProcessingInstruction;
    public string Target { get; }
    public string Data
    {
        get => _data;
        set => ReplaceDataCore(value, 0, BoundaryOrder.GetLength(new DomNodeIdentity(this)), (uint) (value?.Length ?? 0));
    }

    internal void ReplaceDataCore(string value, uint offset, uint count, uint insertedLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        var matches = MutationTracking.Match(this, MutationRecordKind.CharacterData);
        var oldValue = matches?.NeedsOldValue == true ? _data : null;
        LiveTraversalTracking.ReplaceData(this, offset, count, insertedLength);
        _data = value;
        OwnerDocument!.MarkMutation();
        if (ParentNode is { } parent) HtmlTextAreaMutations.ChildrenChanged(parent);
        MutationTracking.QueueCharacterData(this, oldValue, matches);
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
public class DocumentFragment : Node
{
    internal DocumentFragment(Document owner, Element? host = null) : base(owner) => Host = host;
    public override NodeType NodeType => NodeType.DocumentFragment;
    internal Element? Host { get; }
}
