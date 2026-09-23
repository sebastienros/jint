#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

public class HtmlTokenizerContextTests
{
    private static string Signature(IEnumerable<HtmlToken> tokens)
    {
        var result = new StringBuilder();
        var text = new StringBuilder();
        foreach (var token in tokens)
        {
            if (token.Kind == HtmlTokenKind.Text)
            {
                text.Append(token.Data);
                continue;
            }
            if (text.Length != 0)
            {
                result.Append("T:").Append(text).Append('|');
                text.Clear();
            }
            result.Append(token.Kind switch
            {
                HtmlTokenKind.StartTag => "S:" + token.Name,
                HtmlTokenKind.EndTag => "E:" + token.Name,
                HtmlTokenKind.Comment => "C:" + token.Data,
                HtmlTokenKind.EndOfFile => "EOF",
                _ => throw new AssertionException("Unexpected token kind: " + token.Kind)
            }).Append('|');
        }
        if (text.Length != 0) result.Append("T:").Append(text).Append('|');
        return result.ToString();
    }

    private static string Scan(string source, int chunkSize, int quota, HtmlTextMode? fragmentMode = null,
        Action<HtmlTokenizer, HtmlToken>? afterToken = null, HtmlTokenizerContext context = default)
    {
        var tokenizer = new HtmlTokenizer(context);
        var tokens = new List<HtmlToken>();
        if (fragmentMode is { } mode) tokenizer.InitializeFragmentTextMode(mode);
        for (var i = 0; i < source.Length; i += chunkSize)
        {
            tokenizer.AppendInput(source.Substring(i, Math.Min(chunkSize, source.Length - i)));
            Drain(tokenizer, quota, tokens, afterToken, HtmlReadStatus.NeedInput);
        }
        tokenizer.AppendInput(string.Empty, true);
        Drain(tokenizer, quota, tokens, afterToken, HtmlReadStatus.Complete);
        return Signature(tokens);
    }

    private static string ScanTwoPart(string source, int split, int quota, HtmlTextMode? fragmentMode = null,
        Action<HtmlTokenizer, HtmlToken>? afterToken = null, HtmlTokenizerContext context = default)
    {
        var tokenizer = new HtmlTokenizer(context);
        var tokens = new List<HtmlToken>();
        tokenizer.AppendInput(source[..split]);
        if (fragmentMode is { } mode) tokenizer.InitializeFragmentTextMode(mode);
        Drain(tokenizer, quota, tokens, afterToken, HtmlReadStatus.NeedInput);
        tokenizer.AppendInput(source[split..], true);
        Drain(tokenizer, quota, tokens, afterToken, HtmlReadStatus.Complete);
        return Signature(tokens);
    }

    private static void Drain(HtmlTokenizer tokenizer, int quota, List<HtmlToken> tokens,
        Action<HtmlTokenizer, HtmlToken>? afterToken, HtmlReadStatus terminal)
    {
        for (var i = 0; i < 100_000; i++)
        {
            var status = tokenizer.Read(quota, default, out var token);
            if (status == HtmlReadStatus.Token)
            {
                tokens.Add(token);
                afterToken?.Invoke(tokenizer, token);
                continue;
            }
            if (status == HtmlReadStatus.Yielded) continue;
            Assert.That(status, Is.EqualTo(terminal));
            return;
        }
        Assert.Fail("Tokenizer did not finish.");
    }

    [Test]
    public void CDataContextChangesAtCompleteTagBoundariesAcrossChunksAndQuotas()
    {
        const string source = "<![CDATA[h]]><svg><![CDATA[x\r\ny]]></svg><![CDATA[z]]>";
        const string expected = "C:[CDATA[h]]|S:svg|T:x\ny|E:svg|C:[CDATA[z]]|EOF|";
        static void UpdateContext(HtmlTokenizer tokenizer, HtmlToken token)
        {
            if (token.Kind == HtmlTokenKind.StartTag && token.Name == "svg") tokenizer.SetAllowCData(true);
            if (token.Kind == HtmlTokenKind.EndTag && token.Name == "svg") tokenizer.SetAllowCData(false);
        }
        foreach (var chunkSize in new[] { 1, 2, 3, source.Length })
            foreach (var quota in new[] { 1, 3, 1_000 })
            {
                var diagnostics = new ParseDiagnosticCollector();
                var actual = Scan(source, chunkSize, quota, afterToken: UpdateContext,
                    context: new HtmlTokenizerContext(diagnostics: diagnostics));
                Assert.That(actual, Is.EqualTo(expected), $"chunk={chunkSize}, quota={quota}");
                Assert.That(diagnostics.Items.Select(x => x.Code), Is.EqualTo(new[]
                {
                    "html/cdata-in-html-content", "html/cdata-in-html-content"
                }), $"chunk={chunkSize}, quota={quota}");
            }

        for (var split = 0; split <= source.Length; split++)
            foreach (var quota in new[] { 1, 3, 1_000 })
            {
                var diagnostics = new ParseDiagnosticCollector();
                var actual = ScanTwoPart(source, split, quota, afterToken: UpdateContext,
                    context: new HtmlTokenizerContext(diagnostics: diagnostics));
                Assert.That(actual, Is.EqualTo(expected), $"split={split}, quota={quota}");
                Assert.That(diagnostics.Items.Select(x => x.Code), Is.EqualTo(new[]
                {
                    "html/cdata-in-html-content", "html/cdata-in-html-content"
                }), $"split={split}, quota={quota}");
            }
    }

    [Test]
    public void CDataSetterRejectsSuspendedOrPendingTokensWithoutChangingContext()
    {
        var pending = new HtmlTokenizer(default);
        pending.AppendInput("x<svg><![CDATA[y]]>", true);
        Assert.That(pending.Read(1_000, default, out var text), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(text.Kind, Is.EqualTo(HtmlTokenKind.Text));
        Assert.Throws<InvalidOperationException>(() => pending.SetAllowCData(true));
        Assert.That(pending.Read(1_000, default, out var tag), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(tag.Name, Is.EqualTo("svg"));
        pending.SetAllowCData(true);
        var tokens = new List<HtmlToken> { text, tag };
        Drain(pending, 1_000, tokens, null, HtmlReadStatus.Complete);
        Assert.That(Signature(tokens), Is.EqualTo("T:x|S:svg|T:y|EOF|"));

        var reference = new HtmlTokenizer(default);
        reference.AppendInput("&am");
        Assert.That(reference.Read(1_000, default, out _), Is.EqualTo(HtmlReadStatus.NeedInput));
        Assert.Throws<InvalidOperationException>(() => reference.SetAllowCData(true));
        reference.AppendInput("p;<![CDATA[z]]>", true);
        tokens.Clear();
        Drain(reference, 1_000, tokens, null, HtmlReadStatus.Complete);
        Assert.That(Signature(tokens), Is.EqualTo("T:&|C:[CDATA[z]]|EOF|"));

        foreach (var (prefix, suffix) in new[]
                 {
                     ("<![CDA", "TA[x]]>"),
                     ("<![CDATA[x", "]]>"),
                     ("<![CDATA[x]]", ">")
                 })
        {
            var partial = new HtmlTokenizer(new HtmlTokenizerContext(allowCData: true));
            partial.AppendInput(prefix);
            var status = partial.Read(1_000, default, out var partialToken);
            Assert.That(status, Is.AnyOf(HtmlReadStatus.NeedInput, HtmlReadStatus.Token));
            Assert.Throws<InvalidOperationException>(() => partial.SetAllowCData(false));
            partial.AppendInput(suffix, true);
            tokens.Clear();
            if (status == HtmlReadStatus.Token) tokens.Add(partialToken);
            Drain(partial, 1_000, tokens, null, HtmlReadStatus.Complete);
            Assert.That(Signature(tokens), Is.EqualTo("T:x|EOF|"), prefix);
        }

        var empty = new HtmlTokenizer(default);
        Assert.That(empty.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.NeedInput));
        Assert.Throws<InvalidOperationException>(() => empty.SetAllowCData(true));
        empty.AppendInput("<![CDATA[z]]>", true);
        tokens.Clear();
        Drain(empty, 1_000, tokens, null, HtmlReadStatus.Complete);
        Assert.That(Signature(tokens), Is.EqualTo("C:[CDATA[z]]|EOF|"));

        var yielded = new HtmlTokenizer(default);
        yielded.AppendInput("<![CDATA[x]]>", true);
        Assert.That(yielded.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        Assert.Throws<InvalidOperationException>(() => yielded.SetAllowCData(true));
        tokens.Clear();
        Drain(yielded, 1, tokens, null, HtmlReadStatus.Complete);
        Assert.That(Signature(tokens), Is.EqualTo("C:[CDATA[x]]|EOF|"));
    }

    [Test]
    public void CDataSetterRejectsTerminalAndEofSessions()
    {
        var eof = new HtmlTokenizer(default);
        eof.AppendInput("", true);
        Assert.That(eof.Read(1, default, out var token), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(token.Kind, Is.EqualTo(HtmlTokenKind.EndOfFile));
        Assert.Throws<InvalidOperationException>(() => eof.SetAllowCData(true));
        Assert.That(eof.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.Complete));
        Assert.Throws<InvalidOperationException>(() => eof.SetAllowCData(true));

        var limited = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxInputCharacters = 1 }));
        Assert.Throws<ParseLimitException>(() => limited.AppendInput("ab"));
        Assert.Throws<InvalidOperationException>(() => limited.SetAllowCData(true));

        var canceled = new HtmlTokenizer(default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Read(1, cancellation.Token, out _));
        Assert.Throws<InvalidOperationException>(() => canceled.SetAllowCData(true));
    }

    [TestCase(0, "</textarea>&amp;<b>", "E:textarea|T:&|S:b|EOF|")]
    [TestCase(1, "</textarea>&amp;<b>", "T:</textarea>&<b>|EOF|")]
    [TestCase(2, "</style>&amp;<b>", "T:</style>&amp;<b>|EOF|")]
    [TestCase(3, "</script><!--<script>q</script>--><b>", "T:</script><!--<script>q</script>--><b>|EOF|")]
    [TestCase(4, "</plaintext>&amp;<b>", "T:</plaintext>&amp;<b>|EOF|")]
    public void FragmentInitialModeHasNoSyntheticAppropriateEndTag(int modeValue, string source, string expected)
    {
        var mode = (HtmlTextMode) modeValue;
        foreach (var chunkSize in new[] { 1, 3, source.Length })
            foreach (var quota in new[] { 1, 3, 1_000 })
                Assert.That(Scan(source, chunkSize, quota, fragmentMode: mode), Is.EqualTo(expected),
                    $"mode={mode}, chunk={chunkSize}, quota={quota}");
        for (var split = 0; split <= source.Length; split++)
            foreach (var quota in new[] { 1, 3, 1_000 })
                Assert.That(ScanTwoPart(source, split, quota, fragmentMode: mode), Is.EqualTo(expected),
                    $"mode={mode}, split={split}, quota={quota}");
    }

    [Test]
    public void CDataContextUpdatePreservesOrdinaryTextModePermissionInBothOrders()
    {
        foreach (var cdataFirst in new[] { false, true })
        {
            var tokenizer = new HtmlTokenizer(default);
            tokenizer.AppendInput("<textarea>&amp;</textarea><![CDATA[x]]>", true);
            Assert.That(tokenizer.Read(1_000, default, out var start), Is.EqualTo(HtmlReadStatus.Token));
            Assert.That(start.Name, Is.EqualTo("textarea"));

            if (cdataFirst) tokenizer.SetAllowCData(true);
            tokenizer.SetTextMode(HtmlTextMode.RcData, "textarea");
            if (!cdataFirst) tokenizer.SetAllowCData(true);
            Assert.Throws<InvalidOperationException>(() => tokenizer.SetTextMode(HtmlTextMode.Data, null));

            Assert.That(tokenizer.Read(1_000, default, out var text), Is.EqualTo(HtmlReadStatus.Token));
            Assert.That(text.Data, Is.EqualTo("&"));
            Assert.Throws<InvalidOperationException>(() => tokenizer.SetAllowCData(false));

            var tokens = new List<HtmlToken> { start, text };
            Drain(tokenizer, 1_000, tokens, null, HtmlReadStatus.Complete);
            Assert.That(Signature(tokens), Is.EqualTo("S:textarea|T:&|E:textarea|T:x|EOF|"),
                $"cdataFirst={cdataFirst}");
        }
    }

    [Test]
    public void FragmentInitializationAndCDataSettingCoexistBeforeFirstRead()
    {
        foreach (var cdataFirst in new[] { false, true })
        {
            var tokenizer = new HtmlTokenizer(default);
            tokenizer.AppendInput("<![CDATA[x]]>", true);
            if (cdataFirst) tokenizer.SetAllowCData(true);
            tokenizer.InitializeFragmentTextMode(HtmlTextMode.Data);
            if (!cdataFirst) tokenizer.SetAllowCData(true);
            Assert.That(tokenizer.ConsumedInput, Is.Zero);
            Assert.That(tokenizer.WorkCount, Is.Zero);
            var tokens = new List<HtmlToken>();
            Drain(tokenizer, 1, tokens, null, HtmlReadStatus.Complete);
            Assert.That(Signature(tokens), Is.EqualTo("T:x|EOF|"));
        }
    }

    [Test]
    public void FragmentInitializationRejectsInvalidModeOrPhaseWithoutChangingOutput()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("&amp;", true);
        Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.InitializeFragmentTextMode((HtmlTextMode) 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.Read(0, default, out _));
        tokenizer.InitializeFragmentTextMode(HtmlTextMode.RcData);
        Assert.Throws<InvalidOperationException>(() => tokenizer.InitializeFragmentTextMode(HtmlTextMode.Data));
        var tokens = new List<HtmlToken>();
        Drain(tokenizer, 3, tokens, null, HtmlReadStatus.Complete);
        Assert.That(Signature(tokens), Is.EqualTo("T:&|EOF|"));

        var read = new HtmlTokenizer(default);
        read.AppendInput("x");
        Assert.That(read.Read(1_000, default, out var first), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(first.Data, Is.EqualTo("x"));
        Assert.Throws<InvalidOperationException>(() => read.InitializeFragmentTextMode(HtmlTextMode.RawText));
        read.AppendInput("&amp;", true);
        tokens.Clear();
        tokens.Add(first);
        Drain(read, 1_000, tokens, null, HtmlReadStatus.Complete);
        Assert.That(Signature(tokens), Is.EqualTo("T:x&|EOF|"));

        var priorMode = new HtmlTokenizer(default);
        priorMode.SetTextMode(HtmlTextMode.RawText, "style");
        Assert.Throws<InvalidOperationException>(() => priorMode.InitializeFragmentTextMode(HtmlTextMode.Data));
        priorMode.AppendInput("&amp;", true);
        tokens.Clear();
        Drain(priorMode, 1_000, tokens, null, HtmlReadStatus.Complete);
        Assert.That(Signature(tokens), Is.EqualTo("T:&amp;|EOF|"));

        var canceled = new HtmlTokenizer(default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Read(1, cancellation.Token, out _));
        Assert.Throws<InvalidOperationException>(() => canceled.InitializeFragmentTextMode(HtmlTextMode.Data));

        var limited = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxInputCharacters = 1 }));
        Assert.Throws<ParseLimitException>(() => limited.AppendInput("ab"));
        Assert.Throws<InvalidOperationException>(() => limited.InitializeFragmentTextMode(HtmlTextMode.Data));
    }
}
