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

    private static List<HtmlToken> ScanWithReadContext(string source, int split, int quota,
        bool initialCData, Func<HtmlToken, bool, bool> afterToken,
        ParseDiagnosticCollector? diagnostics = null)
    {
        var tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(diagnostics: diagnostics));
        var tokens = new List<HtmlToken>();
        var allowCData = initialCData;
        void DrainPart(HtmlReadStatus expected)
        {
            for (var i = 0; i < 100_000; i++)
            {
                var status = tokenizer.Read(quota, allowCData, default, out var token);
                if (status == HtmlReadStatus.Token)
                {
                    tokens.Add(token);
                    allowCData = afterToken(token, allowCData);
                    continue;
                }
                if (status == HtmlReadStatus.Yielded) continue;
                Assert.That(status, Is.EqualTo(expected));
                return;
            }
            Assert.Fail("Tokenizer did not finish.");
        }
        tokenizer.AppendInput(source[..split]);
        DrainPart(HtmlReadStatus.NeedInput);
        tokenizer.AppendInput(source[split..], true);
        DrainPart(HtmlReadStatus.Complete);
        return tokens;
    }

    [Test]
    public void DataTextIsDeliveredBeforeTheFollowingMarkupOpener()
    {
        const string source = "x<![CDATA[y]]>";
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput(source, true);
        Assert.That(tokenizer.Read(1_000, false, default, out var first), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(first.Kind, Is.EqualTo(HtmlTokenKind.Text));
        Assert.That(first.Data, Is.EqualTo("x"));
        Assert.That(first.Offset, Is.Zero);
        Assert.That(tokenizer.ConsumedInput, Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => tokenizer.SetAllowCData(true));
        var tokens = new List<HtmlToken> { first };
        for (var i = 0; i < 100; i++)
        {
            var status = tokenizer.Read(1_000, true, default, out var token);
            if (status == HtmlReadStatus.Token) { tokens.Add(token); continue; }
            Assert.That(status, Is.EqualTo(HtmlReadStatus.Complete));
            break;
        }
        Assert.That(Signature(tokens), Is.EqualTo("T:xy|EOF|"));
        Assert.That(tokens[1].Offset, Is.EqualTo(10));

        var diagnostics = new ParseDiagnosticCollector();
        var html = ScanWithReadContext(source, source.Length, 1_000, true,
            (token, current) => token.Kind == HtmlTokenKind.Text ? false : current, diagnostics);
        Assert.That(Signature(html), Is.EqualTo("T:x|C:[CDATA[y]]|EOF|"));
        Assert.That(html[1].Offset, Is.EqualTo(1));
        Assert.That(diagnostics.Items.Select(x => (x.Code, x.Offset)),
            Is.EqualTo(new[] { ("html/cdata-in-html-content", 10L) }));
    }

    [Test]
    public void ReadContextFollowsBuilderUpdatesAtEveryShortCDataOpenerSplit()
    {
        const string source = "x<![CDATA[y]]>";
        for (var split = 0; split <= source.Length; split++)
            foreach (var quota in new[] { 1, 3, 1_000 })
            {
                var foreign = ScanWithReadContext(source, split, quota, false,
                    (token, current) => token.Kind == HtmlTokenKind.Text ? true : current);
                Assert.That(Signature(foreign), Is.EqualTo("T:xy|EOF|"), $"foreign split={split}, quota={quota}");
                var html = ScanWithReadContext(source, split, quota, true,
                    (token, current) => token.Kind == HtmlTokenKind.Text ? false : current);
                Assert.That(Signature(html), Is.EqualTo("T:x|C:[CDATA[y]]|EOF|"), $"html split={split}, quota={quota}");
            }
    }

    [Test]
    public void SuspendedReferenceAndDeclarationKeepTheirOwnContexts()
    {
        var reference = new HtmlTokenizer(default);
        reference.AppendInput("x&am");
        Assert.That(reference.Read(1_000, false, default, out var text), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(text.Data, Is.EqualTo("x"));
        Assert.Throws<InvalidOperationException>(() => reference.SetAllowCData(true));
        reference.AppendInput("p;<![CDATA[y]]>", true);
        var tokens = new List<HtmlToken> { text };
        for (var i = 0; i < 100; i++)
        {
            var status = reference.Read(1_000, true, default, out var token);
            if (status == HtmlReadStatus.Token) { tokens.Add(token); continue; }
            Assert.That(status, Is.EqualTo(HtmlReadStatus.Complete));
            break;
        }
        Assert.That(Signature(tokens), Is.EqualTo("T:x&y|EOF|"));

        foreach (var initial in new[] { false, true })
        {
            var declaration = new HtmlTokenizer(default);
            declaration.AppendInput("<![CDA");
            Assert.That(declaration.Read(1_000, initial, default, out _), Is.EqualTo(HtmlReadStatus.NeedInput));
            Assert.Throws<InvalidOperationException>(() => declaration.SetAllowCData(!initial));
            declaration.AppendInput("TA[y]]>", true);
            tokens.Clear();
            for (var i = 0; i < 100; i++)
            {
                var status = declaration.Read(1, !initial, default, out var token);
                if (status == HtmlReadStatus.Token) { tokens.Add(token); continue; }
                if (status == HtmlReadStatus.Yielded) continue;
                Assert.That(status, Is.EqualTo(HtmlReadStatus.Complete));
                break;
            }
            Assert.That(Signature(tokens), Is.EqualTo(initial ? "T:y|EOF|" : "C:[CDATA[y]]|EOF|"));
        }
    }

    [Test]
    public void DeclarationContextIsLatchedWhenTheLessThanSignIsConsumed()
    {
        const string source = "<![CDATA[y]]>";
        foreach (var initial in new[] { false, true })
            foreach (var splitInput in new[] { false, true })
            {
                var tokenizer = new HtmlTokenizer(default);
                tokenizer.AppendInput(splitInput ? "<" : source, !splitInput);
                var status = tokenizer.Read(splitInput ? 1_000 : 1, initial, default, out _);
                Assert.That(status, Is.EqualTo(splitInput ? HtmlReadStatus.NeedInput : HtmlReadStatus.Yielded));
                Assert.That(tokenizer.ConsumedInput, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => tokenizer.SetAllowCData(!initial));
                if (splitInput) tokenizer.AppendInput(source[1..], true);
                var tokens = new List<HtmlToken>();
                for (var i = 0; i < 100; i++)
                {
                    status = tokenizer.Read(3, !initial, default, out var token);
                    if (status == HtmlReadStatus.Token) { tokens.Add(token); continue; }
                    if (status == HtmlReadStatus.Yielded) continue;
                    Assert.That(status, Is.EqualTo(HtmlReadStatus.Complete));
                    break;
                }
                Assert.That(Signature(tokens), Is.EqualTo(initial ? "T:y|EOF|" : "C:[CDATA[y]]|EOF|"),
                    $"initial={initial}, splitInput={splitInput}");
            }
    }

    [Test]
    public void CDataTextFlushDoesNotChangeAnOpenDeclarationOrTheNextOne()
    {
        const int length = 5_000;
        var source = "<![CDATA[" + new string('a', length) + "]]><![CDATA[z]]>";
        foreach (var quota in new[] { 1, 7, 1_000 })
        {
            var tokens = ScanWithReadContext(source, source.Length, quota, true,
                (token, current) => token.Kind == HtmlTokenKind.Text ? false : current);
            Assert.That(string.Concat(tokens.Where(x => x.Kind == HtmlTokenKind.Text).Select(x => x.Data)),
                Is.EqualTo(new string('a', length)), $"quota={quota}");
            Assert.That(tokens.Any(x => x.Kind == HtmlTokenKind.Comment && x.Data == "[CDATA[z]]"),
                Is.True, $"quota={quota}");
            Assert.That(tokens.Where(x => x.Kind == HtmlTokenKind.Text).Max(x => x.Data.Length),
                Is.LessThanOrEqualTo(4096));
        }
    }

    [Test]
    public void ConsecutiveDeclarationsEachLatchTheReadContextAtTheirOpener()
    {
        const string source = "<![CDATA[a]]><![CDATA[b]]>";
        foreach (var split in new[] { 0, 1, 2, 9, 13, source.Length })
        {
            var tokens = ScanWithReadContext(source, split, 1, true,
                (token, current) => token.Kind == HtmlTokenKind.Text ? false : current);
            Assert.That(Signature(tokens), Is.EqualTo("T:a|C:[CDATA[b]]|EOF|"), $"split={split}");
        }
    }

    [Test]
    public void ReadContextPreservesLinearWorkAndTerminalBounds()
    {
        static long Work(int length)
        {
            var tokenizer = new HtmlTokenizer(default);
            tokenizer.AppendInput("x<![CDATA[" + new string('a', length) + "]]>", true);
            var allowCData = false;
            var textLength = 0;
            for (var i = 0; i < 100_000; i++)
            {
                var status = tokenizer.Read(7, allowCData, default, out var token);
                if (status == HtmlReadStatus.Token)
                {
                    if (token.Kind == HtmlTokenKind.Text) { textLength += token.Data.Length; allowCData = true; }
                    continue;
                }
                if (status == HtmlReadStatus.Yielded) continue;
                Assert.That(status, Is.EqualTo(HtmlReadStatus.Complete));
                Assert.That(textLength, Is.EqualTo(length + 1));
                return tokenizer.WorkCount;
            }
            Assert.Fail("Tokenizer did not finish.");
            return 0;
        }
        Assert.That(Work(20_000), Is.LessThan(Work(10_000) * 3));

        var limited = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxTokenCharacters = 8 }));
        limited.AppendInput("x<![CDATA[0123456789]]>", true);
        Assert.That(limited.Read(1_000, false, default, out var first), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(first.Data, Is.EqualTo("x"));
        Assert.Throws<ParseLimitException>(() => limited.Read(1_000, true, default, out _));
        Assert.Throws<InvalidOperationException>(() => limited.Read(1, true, default, out _));

        var canceled = new HtmlTokenizer(default);
        canceled.AppendInput("x<![CDATA[" + new string('a', 20_000) + "]]>", true);
        Assert.That(canceled.Read(1_000, false, default, out var before), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(before.Data, Is.EqualTo("x"));
        Assert.That(canceled.Read(10, true, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Read(10, false, cancellation.Token, out _));
        Assert.Throws<InvalidOperationException>(() => canceled.Read(10, true, default, out _));
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
