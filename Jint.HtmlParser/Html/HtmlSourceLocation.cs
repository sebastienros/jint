namespace Jint.HtmlParser.Html;

// Original UTF-16 coordinates, independent of the expanded-stream diagnostic offset.
// Mixed source retains the post-start-tag anchor, but cannot be mapped to one document.
internal enum HtmlSourceKind { Primary, Inserted, Mixed }

internal readonly record struct HtmlSourceLocation(
    HtmlSourceKind Kind, long SourceUnitId, long Offset, long Line, long Column)
{
    internal HtmlSourceLocation AsMixed() => this with { Kind = HtmlSourceKind.Mixed };
}
