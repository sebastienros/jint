#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint.Constraints;
using Jint.Native.Json;
using Jint.Runtime;
#if NET8_0_OR_GREATER
using Microsoft.Extensions.Time.Testing;
#endif

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="JsonParser"/>'s scanner observes execution constraints, so a long document is interruptible.
/// These pin what those constraints are measured against: a host-issued parse is one run of its own and gets
/// this engine's budget freshly armed, while a parse reached from inside a script keeps spending that
/// script's budget.
/// </summary>
public class HostJsonParseConstraintTests
{
    /// <summary>Longer than the scanner's 10 000-character constraint-check interval, several times over.</summary>
    private static readonly string LongDocument = "{\"a\":\"" + new string('x', 60_000) + "\"}";

    /// <summary>
    /// Reaches the scanner's constraint check more than once. A single long string literal does not: the
    /// scanner jumps straight to the closing quote, so it charges the whole literal and checks one time.
    /// An array charges per element instead, so this one checks at elements 10 000 and 20 000.
    /// </summary>
    private static readonly string RepeatedlyCheckedDocument =
        "[" + string.Join(",", Enumerable.Repeat("1", 25_000)) + "]";

    private const int Budget = 200;

    /// <summary>How many times this run's constraints were armed, and how many times they were consulted.</summary>
    private static (int Resets, int Checks) Tally(Ledger ledger)
    {
        var events = ledger.Read().Split(',');
        return (events.Count(e => e == "Reset"), events.Count(e => e == "Check"));
    }

    /// <summary>
    /// Records the order in which the engine arms and rewinds this run's constraints, which is the mechanism
    /// underneath every other test here and the one thing that needs no clock to observe.
    /// </summary>
    private sealed class Ledger : Constraint
    {
        private readonly List<string> _events = new();

        public string Read()
        {
            lock (_events)
            {
                return string.Join(",", _events);
            }
        }

        public void Clear()
        {
            lock (_events)
            {
                _events.Clear();
            }
        }

        public override void Check()
        {
            lock (_events)
            {
                _events.Add("Check");
            }
        }

        public override void Reset()
        {
            lock (_events)
            {
                _events.Add("Reset");
            }
        }
    }

    [Test]
    public void AHostParseArmsThisRunsConstraintsAndRewindsThemAfterwards()
    {
        var ledger = new Ledger();
        var engine = new Engine(options => options.Constraints.Constraints.Add(ledger));

        engine.Evaluate("1 + 1");
        ledger.Read().Should().StartWith("Reset").And.EndWith("Reset", "a top-level entry brackets itself");
        ledger.Clear();

        new JsonParser(engine).Parse(LongDocument);

        ledger.Read().Should().Be(
            "Reset,Check,Reset",
            "a host-issued parse is a run of its own, so the budget it checks is the one it armed");
    }

    [Test]
    public void AParseReachedFromScriptSpendsTheSurroundingRunsBudget()
    {
        var ledger = new Ledger();
        var engine = new Engine(options => options.Constraints.Constraints.Add(ledger));
        engine.SetValue("doc", LongDocument);
        ledger.Clear();

        engine.Evaluate("JSON.parse(doc).a.length").AsNumber().Should().Be(60_000);

        var (resets, checks) = Tally(ledger);
        resets.Should().Be(
            2,
            "the parse is nested inside the evaluation, so it must arm nothing of its own — one bracket, not two");
        ledger.Read().Should().StartWith("Reset").And.EndWith("Reset");
        checks.Should().BePositive("the parse still consults the surrounding run's budget");
    }

    [TestCase("string")]
    [TestCase("chars")]
    [TestCase("utf8")]
    public void EveryOverloadIsTheSameRun(string overload)
    {
        var ledger = new Ledger();
        var engine = new Engine(options => options.Constraints.Constraints.Add(ledger));
        var parser = new JsonParser(engine);
        ledger.Clear();

        switch (overload)
        {
            case "string":
                parser.Parse(LongDocument);
                break;
            case "chars":
                parser.Parse(LongDocument.AsSpan());
                break;
            case "utf8":
                parser.Parse(Encoding.UTF8.GetBytes(LongDocument).AsSpan());
                break;
        }

        ledger.Read().Should().Be(
            "Reset,Check,Reset",
            "the span overloads are the ones a network or storage caller reaches for");
    }

    [Test]
    public void AHostParseIsBoundedByTheMemoryLimit()
    {
        var document = "[" + string.Join(",", Enumerable.Repeat("{\"a\":1,\"b\":2,\"c\":3}", 50_000)) + "]";

        var generous = new Engine(options => options.LimitMemory(400_000_000));
        Invoking(() => new JsonParser(generous).Parse(document)).Should().NotThrow(
            "the document itself is well inside this budget, so a failure below is the limit and not the load");

        var tight = new Engine(options => options.LimitMemory(4_000_000));
        Invoking(() => new JsonParser(tight).Parse(document))
            .Should().Throw<MemoryLimitExceededException>(
                "a host-issued parse allocates into the engine's realm and is an operation like any other");
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The reported case, made exact with a host clock: the same document, the same engine, the same limit —
    /// and the only difference is that the engine has been used for something else first, and then left idle.
    /// </summary>
    [Test]
    public void AHostParseIsNotBudgetedAgainstADeadlineArmedByAnEarlierRun()
    {
        var clock = new FakeTimeProvider();
        var engine = new Engine(options =>
        {
            options.Constraints.TimeProvider = clock;
            options.LimitExecutionTime(TimeSpan.FromMilliseconds(Budget));
        });

        Invoking(() => new JsonParser(engine).Parse(LongDocument))
            .Should().NotThrow("a never-used engine has no deadline armed");

        engine.Evaluate("1 + 1");
        clock.Advance(TimeSpan.FromSeconds(1));

        Invoking(() => new JsonParser(engine).Parse(LongDocument))
            .Should().NotThrow("the second parse is a run of its own, and its own run has taken no time at all");
    }

    /// <summary>
    /// The other half, and the one a fix could easily break: giving the parse a run of its own must not give
    /// a script a way to refresh the budget it has already spent by calling <c>JSON.parse</c>.
    /// </summary>
    [Test]
    public void AParseInsideAScriptCannotRefreshThatScriptsSpentBudget()
    {
        var clock = new FakeTimeProvider();
        var engine = new Engine(options =>
        {
            options.Constraints.TimeProvider = clock;
            options.LimitExecutionTime(TimeSpan.FromMilliseconds(Budget));
        });

        engine.SetValue("burn", new Action(() => clock.Advance(TimeSpan.FromSeconds(1))));
        engine.SetValue("doc", LongDocument);

        Invoking(() => engine.Evaluate("burn(); JSON.parse(doc).a.length"))
            .Should().Throw<TimeoutException>(
                "the evaluation's budget was gone before the parse started, and the parse is part of it");
    }

    /// <summary>
    /// A host-issued parse is still interruptible: the budget it arms is a real one, not a formality.
    /// </summary>
    [Test]
    public void AHostParseThatOutlivesItsOwnBudgetStillFails()
    {
        var clock = new FakeTimeProvider();
        var untouched = new Engine(options =>
        {
            options.Constraints.TimeProvider = clock;
            options.LimitExecutionTime(TimeSpan.FromMilliseconds(Budget));
        });

        Invoking(() => new JsonParser(untouched).Parse(RepeatedlyCheckedDocument))
            .Should().NotThrow("the same document on a clock nobody moves, so the failure below is the budget");

        // Moves the clock past the budget from inside the parse, on the scanner's first constraint check.
        var ticked = new Engine(options =>
        {
            options.Constraints.TimeProvider = clock;
            options.LimitExecutionTime(TimeSpan.FromMilliseconds(Budget));
            options.Constraints.Constraints.Add(new ClockAdvancingConstraint(clock, TimeSpan.FromSeconds(1)));
        });

        Invoking(() => new JsonParser(ticked).Parse(RepeatedlyCheckedDocument))
            .Should().Throw<TimeoutException>();
    }

    /// <summary>Moves a host clock forward on the first constraint check of a run, and no further.</summary>
    private sealed class ClockAdvancingConstraint : Constraint
    {
        private readonly FakeTimeProvider _clock;
        private readonly TimeSpan _step;
        private bool _advanced;

        public ClockAdvancingConstraint(FakeTimeProvider clock, TimeSpan step)
        {
            _clock = clock;
            _step = step;
        }

        public override void Check()
        {
            if (_advanced)
            {
                return;
            }

            _advanced = true;
            _clock.Advance(_step);
        }

        public override void Reset() => _advanced = false;
    }
#endif
}
