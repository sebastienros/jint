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

    // HTML's matches-about:blank permits a query and fragment; these do not change inheritance.
    internal static bool InheritsCreator(string url)
        => UrlParser.Parse(url) is { Scheme: "about" } parsed
            && (parsed.OpaquePath == "blank" || parsed is { OpaquePath: "srcdoc", Query: null });

    internal static bool MatchesAboutBlank(string url)
        => UrlParser.Parse(url) is { Scheme: "about", OpaquePath: "blank" };

    internal bool IsSameOrigin(DomDocumentOrigin other)
        => ReferenceEquals(this, other) || !IsOpaque && !other.IsOpaque && Serialized == other.Serialized;
}
