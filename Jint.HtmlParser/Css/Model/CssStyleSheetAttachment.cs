namespace Jint.HtmlParser.Css.Model;

// Loader-assigned metadata. Parsing never infers ownership or performs a fetch.
internal sealed record CssStyleSheetAttachment
{
    internal Uri? SourceUrl { get; init; }
    internal Uri? BaseUrl { get; init; }
    internal Node? OwnerNode { get; init; }
    internal CssImportRule? ImportOwner { get; init; }
}
