#nullable enable

using System.Diagnostics;
using System.Text.RegularExpressions;
using Jint.Runtime.RegExp;
using Jint.Runtime.RegExp.GregsEx;

namespace Jint.Tests.Runtime;

/// <summary>
/// A search's regex timeout, when longer than a millisecond, starts at the matcher's first clock read, after the first
/// stride of work, instead of with a clock read for every search (GregsEx <c>RunBudget</c>'s deferred start). A search
/// that ends sooner reads no clock; one that does not must still end in <see cref="RegexMatchTimeoutException"/>, from
/// every entry that searches, with the interval the engine was configured with.
/// </summary>
public class RegExpTimeoutTests
{
    // Exponential for a backtracker, and no required-text or end-of-input check rejects the subject before it starts.
    private const string Catastrophic = "^(a+)+b$";

    // The same without a capture group, so the split and global lanes that read only match ranges take it.
    private const string CatastrophicWithoutCaptures = "^(?:a|aa)+b$";

    // A search that has to grow its backtracking storage, so it is retried: the lookahead keeps the pattern on the VM.
    private const string NeedsFrames = "\"(?:[^\"\\\\]|\\\\.)*(?=\")\"";

    private static readonly string Subject = new string('a', 64) + "bb";

    private static readonly TimeSpan Small = TimeSpan.FromMilliseconds(50);

    private static RegexMatchTimeoutException TimedOut(Engine engine, string script)
    {
        var act = () => engine.Execute(script);
        return act.Should().ThrowExactly<RegexMatchTimeoutException>().Which;
    }

    // Each row is one of the entries that hands the matcher a timeout: test, exec, search, a global match, a global
    // replace with a string and with a function, a split with and without capture groups, and matchAll.
    [TestCase("/" + Catastrophic + "/.test(S)")]
    [TestCase("/" + Catastrophic + "/.exec(S)")]
    [TestCase("S.match(/" + Catastrophic + "/)")]
    [TestCase("S.match(/" + Catastrophic + "/g)")]
    [TestCase("S.search(/" + Catastrophic + "/)")]
    [TestCase("S.replace(/" + Catastrophic + "/g, 'x')")]
    [TestCase("S.replace(/" + Catastrophic + "/g, m => 'x')")]
    [TestCase("S.split(/" + Catastrophic + "/)")]
    [TestCase("S.split(/" + CatastrophicWithoutCaptures + "/)")]
    [TestCase("S.replace(/" + CatastrophicWithoutCaptures + "/g, 'x')")]
    [TestCase("[...S.matchAll(/" + Catastrophic + "/g)]")]
    [TestCase("new RegExp('" + Catastrophic + "').test(S)")]
    public void ACatastrophicSearchEndsInTheConfiguredTimeoutFromEveryEntry(string script)
    {
        var engine = new Engine(o => o.Constraints.RegexTimeout = Small);
        engine.SetValue("S", Subject);

        TimedOut(engine, script).MatchTimeout.Should().Be(Small);
    }

    [Test]
    public void TheDefaultEngineRunsEverySearchUnderTenSeconds()
    {
        // Pinned by value: waiting the interval out would put ten seconds in the suite on every target, and a wall-clock
        // number is either the assertion or a wedge ceiling. What the interval does once it reaches the matcher is the
        // rows above, with an interval short enough to wait for.
        var engine = new Engine();

        Engine.RegexConversionOptions.ResolveTimeout(null, engine).Should().Be(TimeSpan.FromSeconds(10));
        engine.Evaluate("/^(a+)+b$/.test('aaab')").AsBoolean().Should().BeTrue();
    }

    [TestCase(1)]
    [TestCase(10)]
    [TestCase(250)]
    public void ASearchShorterThanItsFirstStrideIsUnaffectedByTheTimeout(int milliseconds)
    {
        // Such a search checks no deadline, so whatever else it costs (the first call's JIT included) is no part of the
        // timeout it runs under. At 1 ms that holds because the interval is not deferred: it is an absolute deadline
        // read at the start of the search and first checked at the first stride.
        var engine = new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(milliseconds));

        engine.Evaluate("/a(b+)c/.test('xabbbcx')").AsBoolean().Should().BeTrue();
        engine.Evaluate("'a-b-c'.split(/-/).join()").AsString().Should().Be("a,b,c");
        engine.Evaluate("'abab'.replace(/b/g, 'X')").AsString().Should().Be("aXaX");
        engine.Evaluate("'abab'.replace(/(b)/g, (m, g) => g.toUpperCase())").AsString().Should().Be("aBaB");
    }

    // Built from ticks: on .NET Framework TimeSpan.FromMilliseconds rounds to whole milliseconds, so a fractional one is
    // TimeSpan.Zero there, which means untimed, and a catastrophic search under it never ends.
    [TestCase(100L)]
    [TestCase(JintRegExpEngine.DeferredStartAbove)]
    public void ATimeoutOfAMillisecondOrLessIsMeasuredFromTheSearchStartAndStillEndsACatastrophicOne(long ticks)
    {
        // Not deferred (see JintRegExpEngine.DeferredStartAbove): the deadline comes from a clock read at the start of the
        // search and is first checked at the first stride, as it was before the budget could defer its start.
        var timeout = TimeSpan.FromTicks(ticks);
        var engine = new Engine(o => o.Constraints.RegexTimeout = timeout);
        engine.SetValue("S", Subject);

        TimedOut(engine, "/" + Catastrophic + "/.test(S)").MatchTimeout.Should().Be(timeout);
    }

    [Test]
    public void ATimeoutThatCannotFireIsNoTimeout()
    {
        // Normalized to untimed before the matcher sees it, so the search is not charged against a clock at all.
        foreach (var timeout in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.MaxValue })
        {
            var engine = new Engine(o => o.Constraints.RegexTimeout = timeout);
            engine.Evaluate("/^(a+)+b$/.test('aaaaaaaaaaaaaaaaaaaaaaaaab')").AsBoolean().Should().BeTrue();
        }
    }

    [Test]
    public void TheMatcherEndsACatastrophicSearchInTheTimeoutItIsGivenFromEveryEntry()
    {
        var matcher = JintRegExpEngine.Compile(Catastrophic, RegExpFlags.None);
        var splitter = JintRegExpEngine.Compile(CatastrophicWithoutCaptures, RegExpFlags.None);
        var groups = new RegexCapture[matcher.CaptureCount];

        // Deferred (50 ms) and not (1 ms, the longest interval that is not).
        foreach (var timeout in new[] { Small, TimeSpan.FromTicks(JintRegExpEngine.DeferredStartAbove) })
        {
            var actions = new Action[]
            {
                () => matcher.TryMatch(Subject, 0, timeout, groups),
                () => matcher.TryFind(Subject, 0, timeout, groups),
                () => matcher.TryMatchRange(Subject, 0, timeout, out _, out _),
                () => matcher.IsMatch(Subject, 0, timeout),
                () => splitter.FindRanges(Subject, 0, timeout, new RegexMatchRange[4]),
            };

            foreach (var action in actions)
            {
                action.Should().Throw<OperationCanceledException>();
            }
        }
    }

    [Test]
    public void TheMatcherAnswersAnOrdinarySearchWithOrWithoutATimeout()
    {
        var matcher = JintRegExpEngine.Compile("a(b+)c", RegExpFlags.None);
        var groups = new RegexCapture[matcher.CaptureCount];

        matcher.IsMatch("xabbcx", 0, Small).Should().BeTrue();
        matcher.IsMatch("xabbcx", 0, null).Should().BeTrue();

        // An interval too short for a clock to resolve is not deferred and checks nothing before the first stride.
        matcher.TryMatch("xabbcx", 0, TimeSpan.FromTicks(1), groups).Should().BeTrue();
        groups[1].Index.Should().Be(2);
        groups[1].Length.Should().Be(2);
        matcher.TryMatch("none", 0, Small, groups).Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase(60_000)]
    public void ASearchThatOutgrowsItsFramesIsRetriedAndAnswersTheSameUnderAnyTimeout(int? milliseconds)
    {
        // A search that exhausts the frames it started with is retried with more (see JintRegExpEngine.SearchSlow), which
        // is where a timeout that began at the first attempt's clock read has to be carried over. A fresh matcher each
        // time, so the plan an earlier call grew cannot spare this one the retry, and PlannedFrames says it grew. This
        // proves the retry runs and answers under a timeout; it cannot tell a carried deadline from a restarted one.
        TimeSpan? timeout = milliseconds is { } ms ? TimeSpan.FromMilliseconds(ms) : null;
        var matcher = JintRegExpEngine.Compile(NeedsFrames, RegExpFlags.None);
        var groups = new RegexCapture[matcher.CaptureCount];
        var subject = "\"" + string.Concat(Enumerable.Repeat("a\\\"b", 20_000)) + "\"";
        var before = matcher.PlannedFrames;

        matcher.TryMatch(subject, 0, timeout, groups).Should().BeTrue();
        groups[0].Length.Should().Be(subject.Length);
        matcher.PlannedFrames.Should().BeGreaterThan(before);

        var ranged = JintRegExpEngine.Compile(NeedsFrames, RegExpFlags.None);
        var rangedBefore = ranged.PlannedFrames;
        ranged.TryMatchRange(subject, 0, timeout, out var index, out var length).Should().BeTrue();
        index.Should().Be(0);
        length.Should().Be(subject.Length);
        ranged.PlannedFrames.Should().BeGreaterThan(rangedBefore);

        // FindRanges hands a batch that outgrew its frames to the same slow path.
        var batched = JintRegExpEngine.Compile(NeedsFrames, RegExpFlags.None);
        var batchedBefore = batched.PlannedFrames;
        var ranges = new RegexMatchRange[4];
        batched.FindRanges(subject, 0, timeout, ranges).Should().Be(1);
        ranges[0].Length.Should().Be(subject.Length);
        batched.PlannedFrames.Should().BeGreaterThan(batchedBefore);
    }

    [Test]
    public void AbsoluteDeadlineIsMeasuredFromWhereTheIntervalBegan()
    {
        // The conversion SearchSlow applies once to carry the first attempt's start into every retry: from where the
        // interval began, not from the retry, and never beyond the range of a timestamp. That it is applied, rather than
        // each retry taking the interval afresh, is not pinned here: telling the two apart needs SearchSlow's FramePlan
        // exposed, and the growth test above shows only that the retries happen and answer.
        var interval = TimeSpan.FromSeconds(3);
        var ticks = (long) (interval.TotalSeconds * Stopwatch.Frequency);

        JintRegExpEngine.AbsoluteDeadline(interval, 1_000).Should().Be(1_000 + ticks);
        JintRegExpEngine.AbsoluteDeadline(null, 1_000).Should().Be(long.MaxValue);
        JintRegExpEngine.AbsoluteDeadline(TimeSpan.MaxValue, 1_000).Should().Be(long.MaxValue);

        // An attempt that never read the clock has no start to carry: now is as good as any.
        var before = Stopwatch.GetTimestamp();
        var deadline = JintRegExpEngine.AbsoluteDeadline(interval, 0);
        deadline.Should().BeInRange(before + ticks, Stopwatch.GetTimestamp() + ticks);
    }
}
