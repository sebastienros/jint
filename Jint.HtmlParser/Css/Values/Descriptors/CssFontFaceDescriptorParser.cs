using System.Text;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.HtmlParser.Css.Values.Descriptors;

// https://drafts.csswg.org/css-fonts-4/#font-resources (Editor's Draft, 13 September 2026).
internal static class CssFontFaceDescriptorParser
{
    private const string Formats = "collection embedded-opentype opentype svg truetype woff woff2";
    private static readonly string[] FormatNames = Formats.Split(' ');
    private const string Technologies = "features-opentype features-aat features-graphite color-colrv0 color-colrv1 color-svg color-sbix color-cbdt variations palettes incremental";
    private const string ReservedFamilies = "initial inherit unset revert revert-layer revert-rule default serif sans-serif cursive fantasy monospace system-ui ui-serif ui-sans-serif ui-monospace ui-rounded emoji math fangsong caption icon menu message-box small-caption status-bar";

    internal static CssPropertyResult Parse(string name, CssReferenceInput input, CssValueWork work)
    {
        name = CssFontFaceDescriptorCatalog.NormalizeName(name, work);
        if (CssFontFaceDescriptorCatalog.NameFailure(name) is { } failure) return failure;
        var parts = Parts(input.Components, work);
        if (parts.Count == 0) return Invalid();
        if (name == "font-family")
            return Family(parts, work) is { } family ? Accepted(name, family) : Invalid();
        if (name == "src") return Sources(parts, work);
        if (name == "font-display")
            return parts.Count == 1 && CssPropertyParser.Keyword(parts[0], "auto block swap fallback optional", work) is { } display
                ? Accepted(name, display) : Invalid();
        if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], "auto", work) is { } automatic)
            return Accepted(name, automatic);
        if (name == "font-weight") return Weight(input, parts, work);
        if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], "normal italic left right oblique", work) is { } style)
            return Accepted(name, style);
        if (parts.Count is < 2 or > 3 || CssPropertyParser.Keyword(parts[0], "oblique", work) is null) return Invalid();
        var endpoints = new List<CssPropertyValue>();
        for (var i = 1; i < parts.Count; i++)
        {
            work.Charge(1);
            var angle = CssTransformParser.Numeric(parts[i], CssMathProduction.Angle, input.MaxNestingDepth, work,
                range: new CssMathRange(-90, 90));
            if (angle.Status != CssPropertyStatus.Valid) return angle;
            if (angle.Value.Kind == CssPropertyValueKind.Numeric)
            {
                var atom = angle.Value.Numeric;
                var bound = atom.Unit switch { CssUnit.Deg => "90", CssUnit.Grad => "100", CssUnit.Turn => ".25", _ => null };
                if (bound is not null)
                {
                    var range = new CssNumericRange(CssNumber.FromValidatedToken("-" + bound, work), true,
                        CssNumber.FromValidatedToken(bound, work), true);
                    if (!range.Contains(atom.Number, work)) return Invalid();
                }
                else if (CssMathNumbers.ParseFinite(atom.Number, atom.Unit, work) is < -90 or > 90) return Invalid();
            }
            endpoints.Add(angle.Value);
        }
        return Accepted(name, "oblique " + string.Join(" ", endpoints.Select(value => value.Serialize())), endpoints: endpoints.AsReadOnly());
    }

    private static CssPropertyResult Weight(CssReferenceInput input, List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count is < 1 or > 2) return Invalid();
        var values = new List<CssPropertyValue>();
        foreach (var part in parts)
        {
            work.Charge(1);
            if (CssPropertyParser.Keyword(part, "bolder lighter", work) is not null) return Invalid();
            var parsed = CssFontWeightPropertyParser.ParseComponent(part, input.MaxNestingDepth, work);
            if (parsed.Status != CssPropertyStatus.Valid) return parsed;
            values.Add(parsed.Value);
        }
        return Accepted("font-weight", string.Join(" ", values.Select(value => value.Serialize())), endpoints: values.AsReadOnly());
    }

    private static List<CssComponentValue> Parts(CssComponentValueList values, CssValueWork work)
    {
        var result = new List<CssComponentValue>();
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Whitespace) result.Add(value);
        }
        return result;
    }

    private static string? Family(List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 1 && parts[0].Kind == CssComponentKind.Token && parts[0].Token.Kind == CssTokenKind.String)
            return Quote(parts[0].Token.Text, work);
        if (parts.Count == 0) return null;
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            work.Charge(1);
            if (part.Kind != CssComponentKind.Token || part.Token.Kind != CssTokenKind.Ident ||
                CssPropertyParser.Keyword(part, ReservedFamilies, work) is not null) return null;
            if (builder.Length != 0) builder.Append(' ');
            builder.Append(CssSyntaxSerializer.SerializeIdentifier(part.Token.Text, work));
        }
        return builder.ToString();
    }

    private static CssPropertyResult Sources(List<CssComponentValue> parts, CssValueWork work)
    {
        var sources = new List<CssFontSource>();
        var entry = new List<CssComponentValue>();
        for (var i = 0; i <= parts.Count; i++)
        {
            work.Charge(1);
            if (i == parts.Count || parts[i].Kind == CssComponentKind.Token && parts[i].Token.Kind == CssTokenKind.Comma)
            {
                if (Source(entry, work) is { } source) sources.Add(source);
                entry.Clear();
            }
            else entry.Add(parts[i]);
        }
        if (sources.Count == 0) return Invalid();
        var text = string.Join(", ", sources.Select(source => source.Text));
        work.Charge(text.Length);
        return Accepted("src", text, sources.AsReadOnly());
    }

    private static CssFontSource? Source(List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 0) return null;
        var first = parts[0];
        if (Function(first, "local"))
        {
            var arguments = Parts(first.Values, work);
            if (parts.Count != 1 || Family(arguments, work) is null) return null;
            var name = string.Join(" ", arguments.Select(argument => argument.Token.Text));
            work.Charge(name.Length);
            return new(CssFontSourceKind.Local, name, null, Array.Empty<string>(), "local(" + Quote(name, work) + ")");
        }
        string url;
        if (first.Kind == CssComponentKind.Token && first.Token.Kind == CssTokenKind.Url) url = first.Token.Text;
        else if (Function(first, "url") && Parts(first.Values, work) is { Count: 1 } args &&
            args[0].Kind == CssComponentKind.Token && args[0].Token.Kind == CssTokenKind.String) url = args[0].Token.Text;
        else return null;
        var index = 1;
        string? format = null;
        var technologies = new List<string>();
        var text = "url(" + Quote(url, work) + ")";
        if (index < parts.Count && Function(parts[index], "format"))
        {
            var arguments = Parts(parts[index++].Values, work);
            if (arguments.Count != 1 || arguments[0].Kind != CssComponentKind.Token ||
                arguments[0].Token.Kind is not (CssTokenKind.String or CssTokenKind.Ident)) return null;
            format = CssFontFaceDescriptorCatalog.NormalizeName(arguments[0].Token.Text, work);
            if (arguments[0].Token.Kind == CssTokenKind.String && format.EndsWith("-variations", StringComparison.Ordinal))
            {
                format = format[..^11];
                if (format is not ("woff2" or "woff" or "truetype" or "opentype")) return null;
                technologies.Add("variations");
            }
            if (!FormatNames.Contains(format, StringComparer.Ordinal)) return null;
            // Recognition describes syntax; it does not claim decoder availability or font activation.
            text += " format(" + Quote(format, work) + ")";
        }
        if (index < parts.Count && Function(parts[index], "tech"))
        {
            var arguments = Parts(parts[index++].Values, work);
            if (arguments.Count == 0 || arguments.Count % 2 == 0) return null;
            for (var i = 0; i < arguments.Count; i++)
            {
                work.Charge(1);
                if (i % 2 != 0)
                {
                    if (arguments[i].Kind != CssComponentKind.Token || arguments[i].Token.Kind != CssTokenKind.Comma) return null;
                    continue;
                }
                var technology = CssPropertyParser.Keyword(arguments[i], Technologies, work);
                if (technology is null) return null;
                technologies.Add(technology);
            }
        }
        if (index != parts.Count) return null;
        if (technologies.Count != 0) text += " tech(" + string.Join(", ", technologies) + ")";
        work.Charge(text.Length);
        return new(CssFontSourceKind.Url, url, format, technologies.AsReadOnly(), text);
    }

    private static bool Function(CssComponentValue value, string name) => value.Kind == CssComponentKind.Function &&
        CssAscii.EqualsIgnoreCase(value.FunctionName, name);

    private static string Quote(string text, CssValueWork work)
    {
        var builder = new StringBuilder();
        CssSyntaxSerializer.AppendString(builder, text, work);
        return builder.ToString();
    }

    private static CssPropertyResult Accepted(string name, string text, IReadOnlyList<CssFontSource>? sources = null,
        IReadOnlyList<CssPropertyValue>? endpoints = null) => CssPropertyResult.Accepted(CssPropertyValue.Descriptor(
            new(name, text, sources ?? Array.Empty<CssFontSource>(), endpoints ?? Array.Empty<CssPropertyValue>())));
    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
