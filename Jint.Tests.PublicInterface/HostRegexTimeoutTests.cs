#nullable enable

using System.Text.RegularExpressions;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins that <c>Options.Constraints.RegexTimeout</c> bounds every regular expression the engine runs, not
/// only the ones written as a literal in the source it parsed.
/// </summary>
/// <remarks>
/// <para>
/// A regex built at run time — <c>new RegExp(...)</c>, <c>RegExp.prototype.compile</c>, the implicit
/// coercion behind <c>"x".match("...")</c>, and the sticky splitter <c>@@split</c> rebuilds even from a
/// literal — is adapted by <c>RegExpConstructor.RegExpInitialize</c> rather than by the parser, so it is
/// the one place where the host's configured budget has to be read rather than inherited. It used to be
/// read off Acornima's <c>ParserOptions.RegexTimeout</c>, which Jint never assigns: every engine in the
/// process got the parser package's own default (sebastienros/jint#3431), and an embedder who tightened
/// the constraint for exactly this reason — catastrophic backtracking on a pattern that arrives as data —
/// was bounded by a value it had never chosen and could not change.
/// </para>
/// <para>
/// The witness is <see cref="RegexMatchTimeoutException.MatchTimeout"/> rather than a stopwatch. The
/// exception is constructed from the very interval the matcher was enforcing, so it says <em>which</em>
/// budget fired, exactly, on a loaded runner as well as an idle one — the same discrimination
/// <c>RegExpTests.ShouldHaveRunUnderThePrepareTimeBudget</c> makes for the prepared lane, and for the same
/// reason (#3379): a wall-clock bound cannot separate two candidates whose overshoot scales together.
/// </para>
/// </remarks>
public class HostRegexTimeoutTests
{
    /// <summary>
    /// A pattern that .NET's <see cref="Regex"/> backtracks catastrophically on, and that stays on the
    /// .NET lane rather than being handed to Jint's custom engine — which matters because only the .NET
    /// lane carries the timeout inside the compiled <see cref="Regex"/>, i.e. only there is the value the
    /// adaptation was given the value that is actually enforced.
    /// </summary>
    private const string Catastrophic = "^(a+)+$";

    /// <summary>A subject the pattern cannot match, so every partition of it is tried.</summary>
    private const string Subject = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!";

    /// <summary>
    /// The host's budget. Deliberately unlike any default in the stack — Jint's own 10 s
    /// (<c>ConstraintOptions.RegexTimeout</c>), the parser package's 5 s — so that the witness below names
    /// one and only one source.
    /// </summary>
    private static readonly TimeSpan HostBudget = TimeSpan.FromMilliseconds(400);

    private static Engine BoundedEngine() => new(options => options.Constraints.RegexTimeout = HostBudget);

    private static string Script(string template) => template
        .Replace("{S}", Subject)
        .Replace("{P}", Catastrophic);

    private static RegexMatchTimeoutException ShouldTimeOut(Action action)
        => Invoking(action).Should().ThrowExactly<RegexMatchTimeoutException>().Which;

    private static void ShouldHaveRunUnderTheHostBudget(RegexMatchTimeoutException timedOut)
        => timedOut.MatchTimeout.Should().Be(
            HostBudget,
            "the match must have run under the engine's configured Constraints.RegexTimeout rather than a default the host never chose");

    // Every one of these builds its regex at run time, which is the lane the constraint used to miss.
    // @@split is in the list because it is reachable without writing new RegExp at all: it rebuilds a
    // sticky splitter through the RegExp constructor, so a plain literal leaves the parser's lane on its
    // own.
    [TestCase("'{S}'.match(new RegExp('{P}'))")]
    [TestCase("'{S}'.match(RegExp('{P}'))")]
    [TestCase("'{S}'.match('{P}')")]
    [TestCase("'{S}'.search(new RegExp('{P}'))")]
    [TestCase("'{S}'.replace(new RegExp('{P}'), 'x')")]
    [TestCase("new RegExp('{P}').test('{S}')")]
    [TestCase("new RegExp('{P}').exec('{S}')")]
    [TestCase("Array.from('{S}'.matchAll(new RegExp('{P}', 'g')))")]
    [TestCase("var re = /unrelated/; re.compile('{P}'); re.test('{S}')")]
    [TestCase("'{S}'.split(/{P}/)")]
    public void AScriptBuiltRegexIsBoundedByTheConfiguredRegexTimeout(string template)
    {
        var engine = BoundedEngine();

        ShouldHaveRunUnderTheHostBudget(ShouldTimeOut(() => engine.Evaluate(Script(template))));
    }

    /// <summary>
    /// The control: the literal lane, which reads the same setting through the parser and always has.
    /// Here to say that the matrix above is about where the value comes from, not about the value.
    /// </summary>
    [Test]
    public void ARegexLiteralIsBoundedByTheConfiguredRegexTimeout()
    {
        var engine = BoundedEngine();

        ShouldHaveRunUnderTheHostBudget(ShouldTimeOut(() => engine.Evaluate(Script("'{S}'.match(/{P}/)"))));
    }

    /// <summary>
    /// A regex built at run time is bounded the same way in a module as in a script — the two carry
    /// separate parsing options, so the setting has to reach both.
    /// </summary>
    [Test]
    public void AModuleBuiltRegexIsBoundedByTheConfiguredRegexTimeout()
    {
        var engine = BoundedEngine();
        engine.Modules.Add("main", Script("export default '{S}'.match(new RegExp('{P}'))"));

        ShouldHaveRunUnderTheHostBudget(ShouldTimeOut(() => engine.Modules.Import("main")));
    }

    /// <summary>
    /// Two engines configured differently get their own budget each, in either order. Jint caches regex
    /// adaptations process-wide, so the question this asks is whether whichever engine ran first decided
    /// for the other — the cache key covers the timeout, and this is what says so from outside.
    /// </summary>
    [Test]
    public void TwoEnginesWithDifferentBudgetsDoNotShareOneAdaptation()
    {
        var tighter = TimeSpan.FromMilliseconds(400);
        var looser = TimeSpan.FromMilliseconds(900);
        var script = Script("'{S}'.match(new RegExp('{P}'))");

        var first = ShouldTimeOut(() => new Engine(o => o.Constraints.RegexTimeout = tighter).Evaluate(script));
        var second = ShouldTimeOut(() => new Engine(o => o.Constraints.RegexTimeout = looser).Evaluate(script));

        first.MatchTimeout.Should().Be(tighter);
        second.MatchTimeout.Should().Be(
            looser,
            "the process-wide adaptation cache is keyed on the timeout, so the first engine to construct a pattern cannot decide the budget of the next one");
    }

    /// <summary>
    /// An explicit per-parse <c>RegexTimeout</c> outranks the engine's constraint, and it has to outrank
    /// it in both lanes or the same script gets two answers.
    /// </summary>
    [Test]
    public void AnExplicitParsingTimeoutOutranksTheConstraintForARuntimeBuiltRegexToo()
    {
        var parsingTimeout = TimeSpan.FromMilliseconds(700);
        var engine = BoundedEngine();
        var parsingOptions = ScriptParsingOptions.Default with { RegexTimeout = parsingTimeout };

        var timedOut = ShouldTimeOut(() => engine.Evaluate(Script("'{S}'.match(new RegExp('{P}'))"), parsingOptions: parsingOptions));

        timedOut.MatchTimeout.Should().Be(
            parsingTimeout,
            "an explicit ScriptParsingOptions.RegexTimeout is documented to take precedence over Constraints.RegexTimeout");
    }

    /// <summary>
    /// A prepared script carries its own timeout, chosen where the preparation happened, and a regex it
    /// builds at run time runs under that one — the same answer its literals already get, so that the two
    /// halves of one prepared script cannot disagree.
    /// </summary>
    [Test]
    public void APreparedScriptBuildsItsRuntimeRegexUnderItsOwnTimeout()
    {
        var prepareTimeout = TimeSpan.FromMilliseconds(700);
        var preparationOptions = ScriptPreparationOptions.Default with
        {
            ParsingOptions = ScriptPreparationOptions.Default.ParsingOptions with { RegexTimeout = prepareTimeout },
        };

        var prepared = Engine.PrepareScript(Script("'{S}'.match(new RegExp('{P}'))"), options: preparationOptions);
        var engine = BoundedEngine();

        var timedOut = ShouldTimeOut(() => engine.Execute(prepared));

        timedOut.MatchTimeout.Should().Be(
            prepareTimeout,
            "a prepared script's own regex timeout governs the regexes it builds, exactly as it governs its literals");
    }

    /// <summary>
    /// A prepared script that chose no timeout of its own runs its literals under the budget of the engine
    /// executing it. Preparation happens where there is no engine, so the value it used to bake in was
    /// Jint's 10 s default — a host that tightened the constraint and then adopted <c>PrepareScript</c>,
    /// the path this repository recommends for production, silently ran at 10 s
    /// (sebastienros/jint#3442).
    /// </summary>
    [Test]
    public void APreparedScriptWithoutItsOwnTimeoutRunsItsLiteralsUnderTheEnginesBudget()
    {
        var prepared = Engine.PrepareScript(Script("'{S}'.match(/{P}/)"));

        ShouldHaveRunUnderTheHostBudget(ShouldTimeOut(() => BoundedEngine().Execute(prepared)));
    }

    /// <summary>
    /// The run-time half of the same script, which #3431 made agree with its literals and which therefore
    /// has to keep agreeing with them here.
    /// </summary>
    [Test]
    public void APreparedScriptWithoutItsOwnTimeoutBuildsItsRuntimeRegexUnderTheEnginesBudget()
    {
        var prepared = Engine.PrepareScript(Script("'{S}'.match(new RegExp('{P}'))"));

        ShouldHaveRunUnderTheHostBudget(ShouldTimeOut(() => BoundedEngine().Execute(prepared)));
    }

    /// <summary>
    /// The same for a prepared module.
    /// </summary>
    [Test]
    public void APreparedModuleWithoutItsOwnTimeoutRunsUnderTheEnginesBudget()
    {
        var prepared = Engine.PrepareModule(Script("export default '{S}'.match(/{P}/)"));
        var engine = BoundedEngine();
        engine.Modules.Add("main", builder => builder.AddModule(prepared));

        ShouldHaveRunUnderTheHostBudget(ShouldTimeOut(() => engine.Modules.Import("main")));
    }

    /// <summary>
    /// One <c>Prepared&lt;Script&gt;</c>, two engines, two budgets — in both orders, because the adapted
    /// regex is memoized on the shared AST node and the hazard is precisely that whichever engine
    /// evaluates the literal first decides for the other.
    /// </summary>
    [TestCase(400, 900)]
    [TestCase(900, 400)]
    public void OnePreparedScriptObservesEachEnginesOwnBudget(int firstMs, int secondMs)
    {
        var first = TimeSpan.FromMilliseconds(firstMs);
        var second = TimeSpan.FromMilliseconds(secondMs);
        var prepared = Engine.PrepareScript(Script("'{S}'.match(/{P}/)"));

        var firstTimedOut = ShouldTimeOut(() => new Engine(o => o.Constraints.RegexTimeout = first).Execute(prepared));
        var secondTimedOut = ShouldTimeOut(() => new Engine(o => o.Constraints.RegexTimeout = second).Execute(prepared));

        firstTimedOut.MatchTimeout.Should().Be(first);
        secondTimedOut.MatchTimeout.Should().Be(
            second,
            "the per-node adaptation memo on a shared prepared script must not let the first engine to reach a literal decide the budget of the next one");
    }

    /// <summary>
    /// The explicit case, from the literal lane: a preparation that chose 2 s keeps 2 s on an engine
    /// configured for 400 ms. Nothing about resolving an unchosen timeout may reach a chosen one.
    /// </summary>
    [Test]
    public void APreparedScriptsOwnTimeoutStillOutranksTheEnginesBudgetForALiteral()
    {
        var prepareTimeout = TimeSpan.FromSeconds(2);
        var preparationOptions = ScriptPreparationOptions.Default with
        {
            ParsingOptions = ScriptPreparationOptions.Default.ParsingOptions with { RegexTimeout = prepareTimeout },
        };

        var prepared = Engine.PrepareScript(Script("'{S}'.match(/{P}/)"), options: preparationOptions);

        var timedOut = ShouldTimeOut(() => BoundedEngine().Execute(prepared));

        timedOut.MatchTimeout.Should().Be(
            prepareTimeout,
            "an explicit preparation-time RegexTimeout is the host having chosen, and outranks the engine's constraint");
    }
}
