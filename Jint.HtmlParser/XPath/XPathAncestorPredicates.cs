using System.Text;
using System.Runtime.ExceptionServices;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Jint.HtmlParser;

// Replace complete bare axis predicates and count calls with unfiltered ancestor axes.
// The framework retains numeric predicate semantics, operators and axis ordering;
// filtered axes, node-set results and caller extension contexts remain untouched.
internal interface IXPathAncestorContext
{
    bool HasAncestor(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf);
    double CountAncestors(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf);
}

internal sealed class XPathAncestorPredicates : XsltContext
{
    private const string InternalNamespace = "urn:jint:native-xpath-ancestor-predicate";
    private readonly IXmlNamespaceResolver? _resolver;
    private readonly string _prefix;
    private readonly List<AncestorFunction> _functions = new();

    private XPathAncestorPredicates(IXmlNamespaceResolver? resolver, string prefix)
    {
        _resolver = resolver;
        _prefix = prefix;
    }

    internal static string? Rewrite(string source, IXmlNamespaceResolver? resolver,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken token, out XPathAncestorPredicates? context)
    {
        context = null;
        // A caller's extension context can supply arbitrary functions/variables and node-set behavior.
        if (resolver is XsltContext) return null;
        var prefix = "jintAncestor";
        if (source.Contains(prefix, StringComparison.Ordinal)) return null;
        StringBuilder? builder = null;
        var copied = 0;
        var quote = '\0';
        var work = 0;
        void Charge()
        {
            if ((++work & 255) != 0) return;
            checkpoint?.Invoke(XPathWorkStage.CompilationScan, work);
            token.ThrowIfCancellationRequested();
        }
        void Space(ref int offset)
        {
            while (offset < source.Length && source[offset] is ' ' or '\t' or '\r' or '\n')
            {
                Charge();
                offset++;
            }
        }

        for (var i = 0; i < source.Length; i++)
        {
            Charge();
            if (quote != '\0')
            {
                if (source[i] == quote) quote = '\0';
                continue;
            }
            if (source[i] is '\'' or '"') { quote = source[i]; continue; }
            var count = source.AsSpan(i).StartsWith("count".AsSpan(), StringComparison.Ordinal) &&
                (i == 0 || !IsNameChar(source[i - 1]));
            if (source[i] != '[' && !count) continue;
            var end = i + (count ? 5 : 1);
            Space(ref end);
            if (count)
            {
                if (end >= source.Length || source[end] != '(') continue;
                end++;
                Space(ref end);
            }
            var includeSelf = source.AsSpan(end).StartsWith("ancestor-or-self".AsSpan(), StringComparison.Ordinal);
            var axis = includeSelf ? "ancestor-or-self" : "ancestor";
            if (!source.AsSpan(end).StartsWith(axis.AsSpan(), StringComparison.Ordinal)) continue;
            end += axis.Length;
            Space(ref end);
            if (!source.AsSpan(end).StartsWith("::".AsSpan(), StringComparison.Ordinal)) continue;
            end += 2;
            Space(ref end);
            var start = end;
            while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] is '_' or '-' or '.' or ':' or '*'))
            {
                Charge();
                end++;
            }
            var test = source[start..end];
            var nodeTest = test == "node" && source.AsSpan(end).StartsWith("()".AsSpan(), StringComparison.Ordinal);
            if (nodeTest) end += 2;
            Space(ref end);
            if (end >= source.Length || source[end] != (count ? ')' : ']') || test.Length == 0) continue;
            var colon = test.IndexOf(':');
            var namePrefix = colon < 0 ? "" : test[..colon];
            var localName = colon < 0 ? test : test[(colon + 1)..];
            try
            {
                if (namePrefix.Length != 0) XmlConvert.VerifyNCName(namePrefix);
                if (localName != "*") XmlConvert.VerifyNCName(localName);
            }
            catch (XmlException) { continue; }
            var uri = namePrefix.Length == 0 ? "" : resolver?.LookupNamespace(namePrefix);
            if (uri is null) continue; // Keep the framework's unresolved-prefix error.
            context ??= new XPathAncestorPredicates(resolver, prefix);
            var function = new AncestorFunction(localName == "*" ? "" : localName, uri,
                test == "*", nodeTest, includeSelf, count);
            var index = context._functions.Count;
            context._functions.Add(function);
            builder ??= new StringBuilder(source.Length);
            builder.Append(source, copied, i - copied);
            if (!count) builder.Append('[');
            builder.Append(prefix).Append(":a").Append(index).Append("()");
            if (!count) builder.Append(']');
            copied = end + 1;
            i = end;
        }

        token.ThrowIfCancellationRequested();
        if (builder is null) return null;
        builder.Append(source, copied, source.Length - copied);
        return builder.ToString();
    }

    private static bool IsNameChar(char value) => char.IsLetterOrDigit(value) ||
        value is '_' or '-' or '.' or ':' or '\u00B7' ||
        char.GetUnicodeCategory(value) is System.Globalization.UnicodeCategory.NonSpacingMark or
            System.Globalization.UnicodeCategory.SpacingCombiningMark or
            System.Globalization.UnicodeCategory.ConnectorPunctuation;

    public override string? LookupNamespace(string prefix)
        => prefix == _prefix ? InternalNamespace : _resolver?.LookupNamespace(prefix) ?? base.LookupNamespace(prefix);
    public override bool Whitespace => true;
    public override bool PreserveWhitespace(XPathNavigator node) => true;
    public override int CompareDocument(string baseUri, string nextbaseUri) => StringComparer.Ordinal.Compare(baseUri, nextbaseUri);
    public override IXsltContextVariable ResolveVariable(string prefix, string name) => throw new XPathException("Unknown XPath variable.");
    public override IXsltContextFunction ResolveFunction(string prefix, string name, XPathResultType[] argTypes)
    {
        if (prefix == _prefix && name.Length > 1 && name[0] == 'a' &&
            int.TryParse(name.AsSpan(1), out var index) && (uint) index < (uint) _functions.Count && argTypes.Length == 0)
        {
            return _functions[index];
        }
        throw new XPathException("Unknown XPath function.");
    }

    internal sealed class EvaluationFailure(Exception error) : Exception
    {
        internal ExceptionDispatchInfo Original { get; } = ExceptionDispatchInfo.Capture(error);
    }

    private sealed class AncestorFunction(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf, bool count) : IXsltContextFunction
    {
        public int Minargs => 0;
        public int Maxargs => 0;
        public XPathResultType ReturnType => count ? XPathResultType.Number : XPathResultType.Boolean;
        public XPathResultType[] ArgTypes => [];
        public object Invoke(XsltContext xsltContext, object[] args, XPathNavigator docContext)
        {
            try
            {
                if (docContext is not IXPathAncestorContext native)
                    throw new XPathException("A native XPath context is required.");
                if (count) return native.CountAncestors(localName, namespaceUri, anyNamespace, nodeTest, includeSelf);
                return native.HasAncestor(localName, namespaceUri, anyNamespace, nodeTest, includeSelf);
            }
            catch (Exception error)
            {
                // System.Xml wraps extension-function exceptions. Keep host limits/cancellation fatal.
                throw new EvaluationFailure(error);
            }
        }
    }
}
