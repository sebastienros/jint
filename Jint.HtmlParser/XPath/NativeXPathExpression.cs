using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser;

// The prepared BCL query is private: callers cannot bypass the guarded source.
internal sealed class NativeXPathExpression
{
    private readonly XPathExpression _prepared;

    private NativeXPathExpression(string source, XPathExpression prepared)
    {
        Source = source;
        _prepared = prepared;
    }

    internal string Source { get; }
    internal XPathResultType ReturnType => _prepared.ReturnType;
    internal XPathExpression ClonePrepared() => _prepared.Clone();

    internal static NativeXPathExpression Compile(string source, IXmlNamespaceResolver? resolver,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source);
        token.ThrowIfCancellationRequested();
        // Validate the caller's exact text before a guard can alter error behavior.
        var original = XPathExpression.Compile(source);
        token.ThrowIfCancellationRequested();
        var guarded = GuardFollowing(source, checkpoint, token);
        token.ThrowIfCancellationRequested();
        var prepared = guarded is null ? original : XPathExpression.Compile(guarded);
        token.ThrowIfCancellationRequested();
        if (resolver is not null)
        {
            prepared.SetContext(resolver);
            token.ThrowIfCancellationRequested();
        }

        return new NativeXPathExpression(source, prepared);
    }

    private static string? GuardFollowing(string source, Action<XPathWorkStage, int>? checkpoint,
        CancellationToken token)
    {
        const string axis = "following";
        const string guard = "self::node()[parent::node()]/";
        StringBuilder? builder = null;
        var copiedThrough = 0;
        var quote = '\0';
        long work = 0;
        void Charge(int units)
        {
            var previous = work;
            work += units;
            if ((work >> 8) == (previous >> 8)) return;
            checkpoint?.Invoke(XPathWorkStage.CompilationScan, (int) Math.Min(work, int.MaxValue));
            token.ThrowIfCancellationRequested();
        }

        for (var i = 0; i < source.Length; i++)
        {
            Charge(1);

            var current = source[i];
            if (quote != '\0')
            {
                if (current == quote) quote = '\0';
                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current != 'f' || i > 0 && IsNameChar(source[i - 1]) ||
                !source.AsSpan(i).StartsWith(axis.AsSpan(), StringComparison.Ordinal)) continue;
            var after = i + axis.Length;
            if (after < source.Length && source[after] != ':' && IsNameChar(source[after])) continue;
            while (after < source.Length && IsXPathSpace(source[after])) after++;
            if (after + 1 >= source.Length || source[after] != ':' || source[after + 1] != ':') continue;

            token.ThrowIfCancellationRequested();
            builder ??= new StringBuilder(source.Length);
            token.ThrowIfCancellationRequested();
            builder.Append(source, copiedThrough, i - copiedThrough);
            Charge(i - copiedThrough);
            builder.Append(guard);
            Charge(guard.Length);
            token.ThrowIfCancellationRequested();
            copiedThrough = i;
        }

        token.ThrowIfCancellationRequested();
        if (builder is null) return null;
        builder.Append(source, copiedThrough, source.Length - copiedThrough);
        Charge(source.Length - copiedThrough);
        token.ThrowIfCancellationRequested();
        var result = builder.ToString();
        Charge(result.Length);
        token.ThrowIfCancellationRequested();
        return result;
    }

    private static bool IsXPathSpace(char value) => value is ' ' or '\t' or '\r' or '\n';

    private static bool IsNameChar(char value) => char.IsLetterOrDigit(value) ||
        value is '_' or '-' or '.' or ':' or '\u00B7' ||
        char.GetUnicodeCategory(value) is System.Globalization.UnicodeCategory.NonSpacingMark or
            System.Globalization.UnicodeCategory.SpacingCombiningMark or
            System.Globalization.UnicodeCategory.ConnectorPunctuation;
}
