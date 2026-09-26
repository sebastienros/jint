using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Descriptors;

// Passive source descriptions contain author URLs/names, never resolved resources or font handles.
internal enum CssFontSourceKind { Url, Local }
internal sealed record CssFontSource(CssFontSourceKind Kind, string Name, string? Format,
    IReadOnlyList<string> Technologies, string Text);
internal sealed record CssFontFaceDescriptorValue(string Name, string Text,
    IReadOnlyList<CssFontSource> Sources, IReadOnlyList<CssPropertyValue> Endpoints);
