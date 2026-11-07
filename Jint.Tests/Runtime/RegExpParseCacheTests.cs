#nullable enable

using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Runtime.RegExp;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins what the process-wide <c>RegExpParseCache</c> shares between engines and what it refuses to: two engines
/// compiling one pattern get one <see cref="JintRegExpEngine"/> once the pattern has been seen twice, through every
/// construction lane, and the regex timeout bounds the compile work an engine does rather than which matchers it may
/// be handed.
/// </summary>
/// <remarks>
/// The cache is process state, so these tests use patterns no other fixture can be compiling and run in the
/// single-worker shift: the bound they exercise is a count of admitted entries, which a parallel fixture would move.
/// </remarks>
[NonParallelizable]
public class RegExpParseCacheTests
{
    private static string UniquePattern() => "cache" + Guid.NewGuid().ToString("N");

    private static JintRegExpEngine MatcherOf(JsValue value) => value.Should().BeOfType<JsRegExp>().Which.Matcher!;

    [Test]
    public void ARuntimeBuiltPatternIsSharedAcrossEnginesFromItsSecondCompile()
    {
        var pattern = UniquePattern();
        var matchers = Enumerable.Range(0, 4)
            .Select(_ => MatcherOf(new Engine().Evaluate($"new RegExp('{pattern}', 'g')")))
            .ToList();

        // The first compile is a sighting, not an admission: a pattern built once from data must not fill the cache.
        matchers[1].Should().NotBeSameAs(matchers[0]);
        // The second compile admits it, and every engine after that is handed the same instance.
        matchers[2].Should().BeSameAs(matchers[1]);
        matchers[3].Should().BeSameAs(matchers[1]);
    }

    [Test]
    public void ALiteralParsedFromSourceIsSharedAcrossEnginesFromItsSecondCompile()
    {
        var pattern = UniquePattern();
        var matchers = Enumerable.Range(0, 3)
            .Select(_ => MatcherOf(new Engine().Evaluate($"/{pattern}/i")))
            .ToList();

        matchers[1].Should().NotBeSameAs(matchers[0]);
        matchers[2].Should().BeSameAs(matchers[1]);
    }

    [Test]
    public void ALiteralInASharedPreparedScriptIsSharedThroughItsNodeMemoFromTheFirstRun()
    {
        var pattern = UniquePattern();
        var prepared = Engine.PrepareScript($"/{pattern}/");
        var literal = prepared.Program!.Body[0].Should().BeAssignableTo<ExpressionStatement>().Which
            .Expression.Should().BeOfType<RegExpLiteral>().Which;

        var first = MatcherOf(new Engine().Evaluate(prepared));
        var second = MatcherOf(new Engine().Evaluate(prepared));

        // The node memo, not the cache, is what serves a shared prepared script: no second sighting is needed.
        second.Should().BeSameAs(first);
        literal.UserData.Should().BeSameAs(first);
    }

    [Test]
    public void TheFlagsArePartOfTheKey()
    {
        var pattern = UniquePattern();
        for (var i = 0; i < 2; i++)
        {
            new Engine().Evaluate($"new RegExp('{pattern}'); new RegExp('{pattern}', 'i')");
        }

        var plain = MatcherOf(new Engine().Evaluate($"new RegExp('{pattern}')"));
        var ignoreCase = MatcherOf(new Engine().Evaluate($"new RegExp('{pattern}', 'i')"));

        ignoreCase.Should().NotBeSameAs(plain);
        MatcherOf(new Engine().Evaluate($"new RegExp('{pattern}')")).Should().BeSameAs(plain);
    }

    [Test]
    public void TheRegexTimeoutIsNotPartOfTheKey()
    {
        var pattern = UniquePattern();
        var budgets = new[] { 400, 900, 400, 900 };
        var matchers = budgets
            .Select(ms => MatcherOf(new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(ms)).Evaluate($"new RegExp('{pattern}')")))
            .ToList();

        // A matcher embeds no timeout - every match re-reads its engine's budget - so engines with different budgets
        // share one instance just as engines with the same budget do.
        matchers[2].Should().BeSameAs(matchers[1]);
        matchers[3].Should().BeSameAs(matchers[1]);
    }

    /// <summary>
    /// A pattern whose compile charges more work than the budget floor a 1 ms timeout allows (see
    /// <c>JintRegExpEngine.MinimumCompileWork</c>), and far less than a default engine's ten seconds allow.
    /// </summary>
    private static string HeavyPattern() => $"'[\\\\p{{L}}--\\\\p{{Lu}}]'.repeat({HeavyClasses}) + '{UniquePattern()}'";

    private const int HeavyClasses = 1500;

    [Test]
    public void ACompileThatOutranItsBudgetIsNotCachedAndDoesNotPoisonAnotherEngine()
    {
        var heavy = HeavyPattern();
        var tight = new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(1));
        var roomy = new Engine();

        Invoking(() => tight.Evaluate($"new RegExp({heavy}, 'iv')")).Should().ThrowExactly<RegexMatchTimeoutException>();

        // The failure left nothing behind: the next engine compiles the pattern itself, under its own budget.
        var compiled = MatcherOf(roomy.Evaluate($"new RegExp({heavy}, 'iv')"));
        compiled.Should().NotBeNull();
    }

    [Test]
    public void AMatcherAnotherEngineCompiledIsServedWhateverTheBudgetOfTheEngineAskingForIt()
    {
        var heavy = HeavyPattern();
        var tight = new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(1));

        Invoking(() => tight.Evaluate($"new RegExp({heavy}, 'iv')")).Should().ThrowExactly<RegexMatchTimeoutException>();

        // Two sightings from engines whose budget allows the compile admit the matcher.
        new Engine().Evaluate($"new RegExp({heavy}, 'iv')");
        var shared = MatcherOf(new Engine().Evaluate($"new RegExp({heavy}, 'iv')"));

        // The budget bounds the compile work this engine performs; a matcher it is handed costs it none, and every
        // match it runs is still bounded by its own timeout, read from the engine at match time.
        MatcherOf(tight.Evaluate($"new RegExp({heavy}, 'iv')")).Should().BeSameAs(shared);
    }

    [Test]
    public void AHotPatternSurvivesAGenerationBoundaryAndAOneShotFloodIsNeverAdmitted()
    {
        var hot = UniquePattern();
        new Engine().Evaluate($"new RegExp('{hot}')");
        var admitted = MatcherOf(new Engine().Evaluate($"new RegExp('{hot}')"));

        // Many more distinct patterns than one generation holds, each seen exactly once: none is admitted, so none
        // can push the hot pattern out.
        var flood = new Engine();
        flood.Evaluate("for (var i = 0; i < 1000; i++) new RegExp('flood" + Guid.NewGuid().ToString("N") + "' + i)");
        MatcherOf(new Engine().Evaluate($"new RegExp('{hot}')")).Should().BeSameAs(admitted);

        // Seen twice, a generation's worth of patterns is admitted, which retires the current generation exactly once
        // wherever its count stood; the hot pattern is still found in the retired generation and carried forward
        // rather than recompiled.
        var churn = new Engine();
        var prefix = "churn" + Guid.NewGuid().ToString("N");
        churn.Evaluate($"for (var i = 0; i < {Jint.Native.RegExp.RegExpParseCache.Capacity}; i++) {{ new RegExp('{prefix}' + i); new RegExp('{prefix}' + i); }}");
        MatcherOf(new Engine().Evaluate($"new RegExp('{hot}')")).Should().BeSameAs(admitted);
    }
}
