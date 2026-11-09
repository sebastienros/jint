using System.Text.RegularExpressions;
using Jint.Runtime.RegExp;
using Jint.Runtime.RegExp.GregsEx;

namespace Jint.Tests.Runtime;

/// <summary>
/// A search's regex timeout starts at the matcher's first clock read, after the first stride of work, instead of with a
/// clock read for every search (GregsEx <c>RunBudget</c>'s deferred start). A search that ends sooner reads no clock;
/// one that does not must still end in <see cref="RegexMatchTimeoutException"/> under the default timeout and under a
/// small one, from every entry that searches, with the interval the engine was configured with.
/// </summary>
public class RegExpTimeoutTests
{
    // Exponential for a backtracker, and no required-text or end-of-input check rejects the subject before it starts.
    private const string Catastrophic = "^(a+)+b$";

    // The same without a capture group, so the split and global lanes that read only match ranges take it.
    private const string CatastrophicWithoutCaptures = "^(?:a|aa)+b$";

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
    public void ACatastrophicSearchEndsInTheDefaultTimeout()
    {
        // The default is ten seconds, so this one waits them out: it is the only row that reads the default itself
        // rather than a value the test chose.
        var engine = new Engine();
        engine.SetValue("S", Subject);

        TimedOut(engine, "/" + Catastrophic + "/.test(S)").MatchTimeout.Should().Be(new ConstraintOptionsDefaults().RegexTimeout);
    }

    [TestCase(10)]
    [TestCase(250)]
    public void ASearchShorterThanItsFirstStrideIsUnaffectedByTheTimeout(int milliseconds)
    {
        // Such a search reads no clock, so whatever else it costs (the first call's JIT included) is no part of the
        // timeout it runs under.
        var engine = new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(milliseconds));

        engine.Evaluate("/a(b+)c/.test('xabbbcx')").AsBoolean().Should().BeTrue();
        engine.Evaluate("'a-b-c'.split(/-/).join()").AsString().Should().Be("a,b,c");
        engine.Evaluate("'abab'.replace(/b/g, 'X')").AsString().Should().Be("aXaX");
        engine.Evaluate("'abab'.replace(/(b)/g, (m, g) => g.toUpperCase())").AsString().Should().Be("aBaB");
    }

    [TestCase(1)]
    [TestCase(0.01)]
    public void ATimeoutOfAMillisecondOrLessIsArmedWithTheSearchAndStillEndsACatastrophicOne(double milliseconds)
    {
        // GregsEx's short-deadline rule: such a timeout is not deferred, so it is exact rather than a stride late.
        var timeout = TimeSpan.FromMilliseconds(milliseconds);
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

        var actions = new Action[]
        {
            () => matcher.TryMatch(Subject, 0, Small, groups),
            () => matcher.TryFind(Subject, 0, Small, groups),
            () => matcher.TryMatchRange(Subject, 0, Small, out _, out _),
            () => matcher.IsMatch(Subject, 0, Small),
            () => splitter.FindRanges(Subject, 0, Small, new RegexMatchRange[4]),
        };

        foreach (var action in actions)
        {
            action.Should().Throw<OperationCanceledException>();
        }
    }

    [Test]
    public void TheMatcherAnswersAnOrdinarySearchWithOrWithoutATimeout()
    {
        var matcher = JintRegExpEngine.Compile("a(b+)c", RegExpFlags.None);
        var groups = new RegexCapture[matcher.CaptureCount];

        matcher.IsMatch("xabbcx", 0, Small).Should().BeTrue();
        matcher.IsMatch("xabbcx", 0, null).Should().BeTrue();
        matcher.TryMatch("xabbcx", 0, TimeSpan.FromSeconds(10), groups).Should().BeTrue();
        groups[1].Index.Should().Be(2);
        groups[1].Length.Should().Be(2);
        matcher.TryMatch("none", 0, Small, groups).Should().BeFalse();
    }

    [Test]
    public void AGrowingSearchAnswersWithAndWithoutATimeout()
    {
        // Backtracking storage that outgrows the first plan retries the search; the retries share one absolute
        // deadline (see JintRegExpEngine.SearchSlow), so growth neither loses an answer nor restarts the timeout.
        var matcher = JintRegExpEngine.Compile("^(?:a|b)*c$", RegExpFlags.None);
        var groups = new RegexCapture[matcher.CaptureCount];
        var subject = string.Concat(Enumerable.Repeat("ab", 30_000)) + "c";

        matcher.TryMatch(subject, 0, null, groups).Should().BeTrue();
        matcher.TryMatch(subject, 0, TimeSpan.FromMinutes(1), groups).Should().BeTrue();
        matcher.TryMatchRange(subject, 0, TimeSpan.FromMinutes(1), out var index, out var length).Should().BeTrue();
        index.Should().Be(0);
        length.Should().Be(subject.Length);

        // ... and the same search, grown on its first batch, under a timeout that cannot be met.
        var ranges = new RegexMatchRange[4];
        var found = matcher.FindRanges(subject, 0, TimeSpan.FromMinutes(1), ranges);
        found.Should().Be(1);
        ranges[0].Length.Should().Be(subject.Length);
    }

    // Stands in for the engine default without restating it: the constraint's own initial value.
    private sealed class ConstraintOptionsDefaults
    {
        public TimeSpan RegexTimeout { get; } = new Options().Constraints.RegexTimeout;
    }
}
