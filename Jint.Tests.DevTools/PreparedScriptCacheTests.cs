using Jint.DevTools;

namespace Jint.Tests.DevTools;

/// <summary>
/// The process's one parse of a script every engine is sent. The cache is shared by every test in the process,
/// so each test uses text of its own and asserts on identity, never on what else the cache holds.
/// </summary>
public sealed class PreparedScriptCacheTests
{
    [Test]
    public void TheSameTextUnderTheSameParseIsOneProgram()
    {
        var text = Script();

        var first = PreparedScriptCache.GetOrPrepare(text, "a.js", 0, strict: false, retainSourceText: true);
        var second = PreparedScriptCache.GetOrPrepare(text, "a.js", 0, strict: false, retainSourceText: true);

        second.Program.Should().BeSameAs(first.Program);
    }

    [Test]
    public void EverythingThatShapesTheParseIsPartOfTheKey()
    {
        var text = Script();
        var program = PreparedScriptCache.GetOrPrepare(text, "a.js", 0, strict: false, retainSourceText: false).Program;

        PreparedScriptCache.GetOrPrepare(text, "b.js", 0, strict: false, retainSourceText: false).Program.Should().NotBeSameAs(program);
        PreparedScriptCache.GetOrPrepare(text, "a.js", 30, strict: false, retainSourceText: false).Program.Should().NotBeSameAs(program);
        PreparedScriptCache.GetOrPrepare(text, "a.js", 0, strict: true, retainSourceText: false).Program.Should().NotBeSameAs(program);
        PreparedScriptCache.GetOrPrepare(text, "a.js", 0, strict: false, retainSourceText: true).Program.Should().NotBeSameAs(program);
    }

    [Test]
    public void AScriptStartingOnADocumentLineReportsThatLine()
    {
        var text = "\n\nthrow new Error('here');" + Padding();
        var prepared = PreparedScriptCache.GetOrPrepare(text, "page.html", 30, strict: false, retainSourceText: true);

        var error = Assert.Throws<Jint.Runtime.JavaScriptException>(() => new Engine().Execute(prepared));

        error!.Location.Start.Line.Should().Be(32);
    }

    [Test]
    public void TwoEnginesRunningOneProgramShareNoState()
    {
        var prepared = PreparedScriptCache.GetOrPrepare("var hits = (globalThis.hits || 0) + 1;" + Padding(), null, 0, false, false);
        var one = new Engine();
        var two = new Engine();

        one.Execute(prepared);
        one.Execute(prepared);
        two.Execute(prepared);

        one.Evaluate("hits").AsNumber().Should().Be(2);
        two.Evaluate("hits").AsNumber().Should().Be(1);
    }

    [Test]
    public void AScriptThatDoesNotParseIsRefusedAndNotCached()
    {
        var text = "function (" + Padding();

        Assert.Throws<ScriptPreparationException>(() => PreparedScriptCache.GetOrPrepare(text, null, 0, false, false));
        Assert.Throws<ScriptPreparationException>(() => PreparedScriptCache.GetOrPrepare(text, null, 0, false, false));
    }

    [Test]
    public void TheLeastRecentlyUsedScriptLeavesFirst()
    {
        // Three scripts of 40% of the bound each: the third cannot fit beside the first two. The second is
        // read again before it arrives, so the first is the one to go.
        var size = (int) (PreparedScriptCache.MaxCachedCharacters * 0.4);
        var (oldest, recent, newest) = (Script(size), Script(size), Script(size));
        var oldestProgram = PreparedScriptCache.GetOrPrepare(oldest, null, 0, false, false).Program;
        var recentProgram = PreparedScriptCache.GetOrPrepare(recent, null, 0, false, false).Program;
        PreparedScriptCache.GetOrPrepare(oldest, null, 0, false, false);
        PreparedScriptCache.GetOrPrepare(recent, null, 0, false, false);

        PreparedScriptCache.GetOrPrepare(newest, null, 0, false, false);

        PreparedScriptCache.GetOrPrepare(recent, null, 0, false, false).Program.Should().BeSameAs(recentProgram);
        PreparedScriptCache.GetOrPrepare(oldest, null, 0, false, false).Program.Should().NotBeSameAs(oldestProgram);
    }

    [Test]
    public void OnlyAnEngineThatParsesWithTheDefaultsSuitsTheCache()
    {
        PreparedScriptCache.Suits(new Engine().Options).Should().BeTrue();
        PreparedScriptCache.Suits(new Engine(o => o.Parsing.MaxSourceLength = 1_000_000).Options).Should().BeFalse();
        PreparedScriptCache.Suits(new Engine(o => o.Parsing.MaxNodeCount = 1_000_000).Options).Should().BeFalse();
        PreparedScriptCache.Suits(new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromSeconds(1)).Options).Should().BeFalse();
    }

    private static string Script(int length = 0)
    {
        var text = "var unique" + Guid.NewGuid().ToString("N") + " = 1;";
        return length > text.Length ? text + new string(' ', length - text.Length) : text + Padding();
    }

    private static string Padding() => new(' ', PreparedScriptCache.MinimumLength);
}
