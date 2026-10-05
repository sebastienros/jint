using System.Text;
using System.Runtime.ExceptionServices;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Jint.HtmlParser;

// Only a complete, bare axis predicate is replaced. Numeric/positional predicates, axis
// results and extension contexts continue through the framework's XPath 1.0 evaluator.
internal interface IXPathAncestorContext
{
    bool HasAncestor(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf);
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
            if (source[i] != '[') continue;
            var end = i + 1;
            Space(ref end);
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
            if (end >= source.Length || source[end] != ']' || test.Length == 0) continue;
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
                test == "*", nodeTest, includeSelf);
            var index = context._functions.Count;
            context._functions.Add(function);
            builder ??= new StringBuilder(source.Length);
            builder.Append(source, copied, i - copied);
            builder.Append('[').Append(prefix).Append(":a").Append(index).Append("()]");
            copied = end + 1;
            i = end;
        }

        token.ThrowIfCancellationRequested();
        if (builder is null) return null;
        builder.Append(source, copied, source.Length - copied);
        return builder.ToString();
    }

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

    private sealed class AncestorFunction(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf) : IXsltContextFunction
    {
        public int Minargs => 0;
        public int Maxargs => 0;
        public XPathResultType ReturnType => XPathResultType.Boolean;
        public XPathResultType[] ArgTypes => [];
        public object Invoke(XsltContext xsltContext, object[] args, XPathNavigator docContext)
        {
            try
            {
                return docContext is IXPathAncestorContext native
                    ? native.HasAncestor(localName, namespaceUri, anyNamespace, nodeTest, includeSelf)
                    : throw new XPathException("A native XPath context is required.");
            }
            catch (Exception error)
            {
                // System.Xml wraps extension-function exceptions. Keep host limits/cancellation fatal.
                throw new EvaluationFailure(error);
            }
        }
    }
}
