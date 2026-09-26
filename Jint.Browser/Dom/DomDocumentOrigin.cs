using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>An immutable HTML origin snapshot; an opaque origin's identity is this instance.</summary>
internal sealed class DomDocumentOrigin
{
    private DomDocumentOrigin(string serialized, string domain)
    {
        Serialized = serialized;
        Domain = domain;
    }

    internal string Serialized { get; }
    internal string Domain { get; }
    internal bool IsOpaque => Serialized == "null";
    internal static DomDocumentOrigin Opaque() => new("null", "");

    internal static DomDocumentOrigin FromUrl(string url)
    {
        var serialized = UrlParser.Parse(url)?.SerializeOrigin();
        return serialized is null or "null" ? Opaque()
            : new DomDocumentOrigin(serialized, UrlParser.Parse(serialized)!.SerializeHost());
    }

    internal bool IsSameOrigin(DomDocumentOrigin other)
        => ReferenceEquals(this, other) || !IsOpaque && !other.IsOpaque && Serialized == other.Serialized;
}
