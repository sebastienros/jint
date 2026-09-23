using System.Xml;

namespace Jint.HtmlParser;

/// <summary>A text node.</summary>
public sealed class Text : Node
{
    internal Text(Document owner, string data) : base(owner) => Data = data ?? throw new ArgumentNullException(nameof(data));
    public override NodeType NodeType => NodeType.Text;
    public string Data { get; set; }
}

/// <summary>A comment node.</summary>
public sealed class Comment : Node
{
    internal Comment(Document owner, string data) : base(owner) => Data = data ?? throw new ArgumentNullException(nameof(data));
    public override NodeType NodeType => NodeType.Comment;
    public string Data { get; set; }
}

/// <summary>An XML CDATA section.</summary>
public sealed class CDataSection : Node
{
    internal CDataSection(Document owner, string data) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Contains("]]>", StringComparison.Ordinal)) throw DomException.InvalidCharacter();
        Data = data;
    }
    public override NodeType NodeType => NodeType.CDataSection;
    public string Data { get; set; }
}

/// <summary>An XML processing instruction.</summary>
public sealed class ProcessingInstruction : Node
{
    internal ProcessingInstruction(Document owner, string target, string data) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(target);
        // DOM Standard §4.13 uses XML's Name production for PI targets.
        try { Target = XmlConvert.VerifyName(target); }
        catch (XmlException) { throw DomException.InvalidCharacter(); }

        ArgumentNullException.ThrowIfNull(data);
        if (data.Contains("?>", StringComparison.Ordinal)) throw DomException.InvalidCharacter();
        Data = data;
    }

    public override NodeType NodeType => NodeType.ProcessingInstruction;
    public string Target { get; }
    public string Data { get; set; }
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
    internal DocumentFragment(Document owner) : base(owner) { }
    public override NodeType NodeType => NodeType.DocumentFragment;
}
