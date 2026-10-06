#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

public class HtmlTokenizerInsertionTests
{
    private static HtmlReadStatus Drain(HtmlTokenizer tokenizer, List<HtmlToken> tokens, int quota,
        HtmlInsertionPoint? point = null, bool allowCData = false)
    {
        for (var i = 0; i < 100_000; i++)
        {
            var status = point is null
                ? tokenizer.Read(quota, allowCData, default, out var token)
                : tokenizer.ReadUntil(point, quota, allowCData, default, out token);
            if (status == HtmlReadStatus.Yielded) continue;
            if (status != HtmlReadStatus.Token) return status;
            tokens.Add(token);
            if (token.Kind == HtmlTokenKind.StartTag)
            {
                if (token.Name == "script") tokenizer.SetTextMode(HtmlTextMode.ScriptData, "script");
                if (token.Name == "style") tokenizer.SetTextMode(HtmlTextMode.RawText, "style");
                if (token.Name == "textarea") tokenizer.SetTextMode(HtmlTextMode.RcData, "textarea");
            }
        }
        Assert.Fail("Tokenizer did not reach a boundary.");
        return default;
    }

    private static string Signature(IEnumerable<HtmlToken> tokens)
    {
        var result = new StringBuilder();
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length == 0) return;
            result.Append("T:").Append(text).Append('|');
            text.Clear();
        }
        foreach (var token in tokens)
        {
            if (token.Kind == HtmlTokenKind.Text) { text.Append(token.Data); continue; }
            Flush();
            result.Append(token.Kind).Append(':').Append(token.Name).Append(':').Append(token.Data);
            foreach (var attribute in token.Attributes) result.Append(' ').Append(attribute.Name).Append('=').Append(attribute.Value);
            result.Append('|');
        }
        Flush();
        return result.ToString();
    }

    // HTML Standard §13.2.3, §13.2.5 and dynamic markup insertion: a marker
    // suspends the stream, including lookahead, without invoking EOF recovery.
    [TestCase("<div a='&amp;'>x</div>")]
    [TestCase("&amp;&#x41;&notin;")]
    [TestCase("<!--x--!><!DOCTYPE html>")]
    [TestCase("<?pi a?b?>")]
    [TestCase("<![CDATA[x]]>")]
    [TestCase("<style>a</style>")]
    [TestCase("<textarea>&amp;</textarea>")]
    [TestCase("<script><!--<script>x</script>--></script>")]
    [TestCase("a\r\nb")]
    [TestCase("ordinary text before\r\nand after a newline with a longer tail")]
    [TestCase("<longtag attribute='ordinary value with &notin; and more text'>longer text</longtag>")]
    public void EveryShortWriteSplitPreservesPartialStateAndOuterTail(string written)
    {
        const string tail = "<outer>tail</outer>";
        var baseline = new HtmlTokenizer(default);
        baseline.AppendInput(written + tail, true);
        var expected = new List<HtmlToken>();
        Assert.That(Drain(baseline, expected, 1_000), Is.EqualTo(HtmlReadStatus.Complete));
        foreach (var quota in new[] { 1, 3, 1_000 })
            for (var split = 0; split <= written.Length; split++)
            {
                var tokenizer = new HtmlTokenizer(default);
                tokenizer.AppendInput(tail, true);
                var point = tokenizer.CreateInsertionPoint();
                var tokens = new List<HtmlToken>();
                tokenizer.InsertInput(point, written[..split], default);
                Assert.That(Drain(tokenizer, tokens, quota, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
                Assert.That(tokens.Any(t => t.Kind == HtmlTokenKind.EndOfFile), Is.False);
                Assert.That(tokenizer.ConsumedInput, Is.LessThanOrEqualTo(split));
                tokenizer.InsertInput(point, written[split..], default);
                Assert.That(Drain(tokenizer, tokens, quota, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
                tokenizer.ReleaseInsertionPoint(point);
                Assert.That(Drain(tokenizer, tokens, quota), Is.EqualTo(HtmlReadStatus.Complete));
                Assert.That(Signature(tokens), Is.EqualTo(Signature(expected)), $"split={split}, quota={quota}");
                Assert.That(tokenizer.ConsumedInput, Is.EqualTo(written.Length + tail.Length));
                Assert.That(tokens.Select(t => t.Offset), Is.Ordered);
            }
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(1_000)]
    public void NestedWritesAndRepeatedUndrainedWritesPreserveOrder(int quota)
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("OUTER", true);
        var outer = tokenizer.CreateInsertionPoint();
        tokenizer.InsertInput(outer, "<script></script>suffix", default);
        var tokens = new List<HtmlToken>();
        while (!tokens.Any(t => t.Kind == HtmlTokenKind.EndTag))
        {
            var status = tokenizer.ReadUntil(outer, quota, false, default, out var token);
            Assert.That(status, Is.AnyOf(HtmlReadStatus.Token, HtmlReadStatus.Yielded));
            if (status != HtmlReadStatus.Token) continue;
            tokens.Add(token);
            if (token.Kind == HtmlTokenKind.StartTag) tokenizer.SetTextMode(HtmlTextMode.ScriptData, "script");
        }
        var nested = tokenizer.CreateInsertionPoint();
        tokenizer.InsertInput(nested, "N1", default);
        tokenizer.InsertInput(nested, "N2", default);
        tokenizer.InsertInput(outer, "W1", default);
        tokenizer.InsertInput(outer, "W2", default);
        Assert.That(Drain(tokenizer, tokens, quota, nested), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        Assert.That(Signature(tokens), Does.EndWith("T:N1N2|"));
        tokenizer.ReleaseInsertionPoint(nested);
        Assert.That(Drain(tokenizer, tokens, quota, outer), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        Assert.That(Signature(tokens), Does.EndWith("T:N1N2suffixW1W2|"));
        tokenizer.ReleaseInsertionPoint(outer);
        Assert.That(Drain(tokenizer, tokens, quota), Is.EqualTo(HtmlReadStatus.Complete));
        Assert.That(Signature(tokens), Does.Contain("T:N1N2suffixW1W2OUTER|"));
    }

    [Test]
    public void SpeculativeDeclarationLookaheadKeepsUnreadMarkerInsertable()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("X", true);
        var point = tokenizer.CreateInsertionPoint();
        tokenizer.InsertInput(point, "<!DO", default);
        Assert.That(tokenizer.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        Assert.That(tokenizer.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        Assert.That(tokenizer.ConsumedInput, Is.EqualTo(2));
        Assert.That(tokenizer.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        Assert.That(tokenizer.ConsumedInput, Is.EqualTo(2));
        tokenizer.InsertInput(point, "CTYPE html>", default);
        var tokens = new List<HtmlToken>();
        Assert.That(Drain(tokenizer, tokens, 1, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        // Ordinary Read has already selected bogus-comment recovery using X;
        // insertion preserves that state rather than reparsing the opener.
        Assert.That(tokens.Single().Kind, Is.EqualTo(HtmlTokenKind.Comment));
        Assert.That(tokens.Single().Data, Is.EqualTo("DOCTYPE html"));
        tokenizer.ReleaseInsertionPoint(point);
        Assert.That(Drain(tokenizer, tokens, 1), Is.EqualTo(HtmlReadStatus.Complete));
        Assert.That(tokens.Single(t => t.Kind == HtmlTokenKind.Text).Data, Is.EqualTo("X"));
    }

    [Test]
    public void InsertionInvalidatesSuspendedLookaheadBeforeTokenDecision()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("X", true);
        var outer = tokenizer.CreateInsertionPoint();
        var markers = new List<HtmlInsertionPoint>();
        for (var i = 0; i < 64; i++) markers.Add(tokenizer.CreateInsertionPoint());
        tokenizer.InsertInput(markers[^1], "<!DO", default);
        for (var i = 0; i < 3; i++)
            Assert.That(tokenizer.ReadUntil(outer, 1, false, default, out _), Is.EqualTo(HtmlReadStatus.Yielded));
        Assert.That(tokenizer.ConsumedInput, Is.EqualTo(2));
        // The distance-2 probe is suspended at this marker. Its saved cursor
        // must be invalidated so the newly spliced source is not skipped.
        tokenizer.InsertInput(markers[^2], "CTYPE html>", default);
        var tokens = new List<HtmlToken>();
        Assert.That(Drain(tokenizer, tokens, 1, outer), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        Assert.That(tokens.Single().Kind, Is.EqualTo(HtmlTokenKind.Doctype));
        Assert.That(tokens.Single().Name, Is.EqualTo("html"));
        Assert.Throws<InvalidOperationException>(() => tokenizer.InsertInput(markers[^1], "stale", default));
        foreach (var marker in markers) tokenizer.ReleaseInsertionPoint(marker);
        tokenizer.ReleaseInsertionPoint(outer);
        Assert.That(Drain(tokenizer, tokens, 1), Is.EqualTo(HtmlReadStatus.Complete));
        Assert.That(tokens.Single(t => t.Kind == HtmlTokenKind.Text).Data, Is.EqualTo("X"));
    }

    [Test]
    public void PendingFinalEofAndEmptyWritesAreDistinctFromEmittedEof()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("", true);
        var point = tokenizer.CreateInsertionPoint();
        tokenizer.InsertInput(point, "", default);
        var tokens = new List<HtmlToken>();
        Assert.That(Drain(tokenizer, tokens, 1, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        Assert.That(tokens, Is.Empty);
        tokenizer.InsertInput(point, "x", default);
        Assert.That(Drain(tokenizer, tokens, 1, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        tokenizer.ReleaseInsertionPoint(point);
        Assert.That(tokenizer.Read(1, default, out var eof), Is.EqualTo(HtmlReadStatus.Token));
        Assert.That(eof.Kind, Is.EqualTo(HtmlTokenKind.EndOfFile));
        Assert.Throws<InvalidOperationException>(() => tokenizer.CreateInsertionPoint());
        Assert.Throws<InvalidOperationException>(() => tokenizer.InsertInput(point, "y", default));
        Assert.That(tokenizer.Read(1, default, out _), Is.EqualTo(HtmlReadStatus.Complete));
    }

    [Test]
    public void ForeignReleasedAndPassedMarkersRejectWithoutChangingInput()
    {
        var first = new HtmlTokenizer(default);
        var second = new HtmlTokenizer(default);
        var point = first.CreateInsertionPoint();
        Assert.Throws<InvalidOperationException>(() => second.InsertInput(point, "x", default));
        Assert.Throws<InvalidOperationException>(() => second.ReadUntil(point, 1, false, default, out _));
        Assert.Throws<InvalidOperationException>(() => second.ReleaseInsertionPoint(point));
        first.ReleaseInsertionPoint(point);
        Assert.Throws<InvalidOperationException>(() => first.ReleaseInsertionPoint(point));
        Assert.Throws<InvalidOperationException>(() => first.ReadUntil(point, 1, false, default, out _));
        Assert.Throws<InvalidOperationException>(() => first.InsertInput(point, "x", default));
        point = first.CreateInsertionPoint();
        first.AppendInput("x");
        var tokens = new List<HtmlToken>();
        Assert.That(Drain(first, tokens, 1), Is.EqualTo(HtmlReadStatus.NeedInput));
        Assert.Throws<InvalidOperationException>(() => first.InsertInput(point, "x", default));
        first.ReleaseInsertionPoint(point);
        Assert.That(Signature(tokens), Is.EqualTo("T:x|"));
    }

    [Test]
    public void InsertionsShareExactInputAndTokenLimitsAndCancellationIsAtomic()
    {
        var exact = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxInputCharacters = 6 }));
        exact.AppendInput("tail", true);
        var exactPoint = exact.CreateInsertionPoint();
        exact.InsertInput(exactPoint, "xy", default);
        var exactTokens = new List<HtmlToken>();
        Assert.That(Drain(exact, exactTokens, 1, exactPoint), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        exact.ReleaseInsertionPoint(exactPoint);
        Assert.That(Drain(exact, exactTokens, 1), Is.EqualTo(HtmlReadStatus.Complete));
        Assert.That(exact.ConsumedInput, Is.EqualTo(6));
        Assert.That(Signature(exactTokens), Does.StartWith("T:xytail|"));

        var tokenizer = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxInputCharacters = 6 }));
        tokenizer.AppendInput("tail", true);
        var point = tokenizer.CreateInsertionPoint();
        tokenizer.InsertInput(point, "xy", default);
        tokenizer.InsertInput(point, "", default);
        var work = tokenizer.WorkCount;
        Assert.Throws<ParseLimitException>(() => tokenizer.InsertInput(point, "z", default));
        Assert.That(tokenizer.ConsumedInput, Is.Zero);
        Assert.That(tokenizer.WorkCount, Is.EqualTo(work));
        Assert.Throws<InvalidOperationException>(() => tokenizer.ReadUntil(point, 1, false, default, out _));
        tokenizer.ReleaseInsertionPoint(point);

        var canceled = new HtmlTokenizer(default);
        canceled.AppendInput("tail", true);
        point = canceled.CreateInsertionPoint();
        work = canceled.WorkCount;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.InsertInput(point, "write", cancellation.Token));
        Assert.That(canceled.ConsumedInput, Is.Zero);
        Assert.That(canceled.WorkCount, Is.EqualTo(work));
        Assert.Throws<InvalidOperationException>(() => canceled.CreateInsertionPoint());
        canceled.ReleaseInsertionPoint(point);

        var limited = new HtmlTokenizer(new HtmlTokenizerContext(new ParseLimits { MaxTokenCharacters = 5 }));
        limited.AppendInput("tail", true);
        point = limited.CreateInsertionPoint();
        limited.InsertInput(point, "<di", default);
        var tokens = new List<HtmlToken>();
        Assert.That(Drain(limited, tokens, 1, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        limited.InsertInput(point, "v x>", default);
        Assert.Throws<ParseLimitException>(() => Drain(limited, tokens, 1, point));
    }

    [Test]
    public void InsertionReadsPreserveDeclarationContextAndStrictSetterContract()
    {
        var tokenizer = new HtmlTokenizer(default);
        tokenizer.AppendInput("TAIL", true);
        var point = tokenizer.CreateInsertionPoint();
        tokenizer.InsertInput(point, "<![CDA", default);
        var tokens = new List<HtmlToken>();
        Assert.That(Drain(tokenizer, tokens, 1, point, true), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        Assert.Throws<InvalidOperationException>(() => tokenizer.SetAllowCData(false));
        tokenizer.InsertInput(point, "TA[x]]>", default);
        Assert.That(Drain(tokenizer, tokens, 3, point, false), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
        Assert.That(Signature(tokens), Is.EqualTo("T:x|"));
    }

    [Test]
    public void ManySmallWritesAndMarkerCleanupHaveLinearChargedWork()
    {
        static long Work(int count, bool drainEach)
        {
            var tokenizer = new HtmlTokenizer(default);
            tokenizer.AppendInput("CTYPE html>", true);
            var point = tokenizer.CreateInsertionPoint();
            var tokens = new List<HtmlToken>();
            for (var i = 0; i < count; i++)
            {
                tokenizer.InsertInput(point, "x", default);
                if (drainEach) Assert.That(Drain(tokenizer, tokens, 1, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
            }
            Assert.That(Drain(tokenizer, tokens, 1, point), Is.EqualTo(HtmlReadStatus.InsertionBoundary));
            tokenizer.ReleaseInsertionPoint(point);
            // Many markers in declaration lookahead must also make quota-1 progress.
            var markers = new List<HtmlInsertionPoint>();
            for (var i = 0; i < count; i++) markers.Add(tokenizer.CreateInsertionPoint());
            tokenizer.InsertInput(markers[^1], "<!DO", default);
            Assert.That(Drain(tokenizer, tokens, 1), Is.EqualTo(HtmlReadStatus.Complete));
            Assert.That(tokens.Single(t => t.Kind == HtmlTokenKind.Doctype).Name, Is.EqualTo("html"));
            foreach (var marker in markers) tokenizer.ReleaseInsertionPoint(marker);
            Assert.That(tokens.Where(t => t.Kind == HtmlTokenKind.Text).Sum(t => t.Data.Length), Is.EqualTo(count));
            return tokenizer.WorkCount;
        }
        foreach (var drainEach in new[] { false, true })
            Assert.That(Work(2_000, drainEach), Is.LessThan(Work(1_000, drainEach) * 3));
    }
}
