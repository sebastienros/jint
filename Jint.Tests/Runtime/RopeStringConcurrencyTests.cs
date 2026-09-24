#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Several string representations memoize their text on first read, but a
/// <see cref="JsString.RopeString"/> is the one whose first read also takes something away: flattening
/// releases the two operands, so that a flattened value stops retaining the tree it was built from. A walk
/// that is still descending those operands on another thread must not find them gone. Before the
/// representation existed, the result of a long <c>a + b</c> was a flat string with nothing to write after
/// construction, and reading it from several threads at once was a non-event (sebastienros/jint#4161).
/// <para>
/// It still has to be. The host does not have to share a result to get there: a
/// <see cref="Prepared{T}"/> is documented as safe to run on several engines at once, and preparation folds
/// a literal-plus-literal into one constant published on the shared tree — so every engine running the
/// preparation reads the same node, and the first of those reads is concurrent whenever the engines are.
/// These tests pin both halves: the node itself, read first from two threads, and the prepared-script
/// shape that reaches it without any host involvement.
/// </para>
/// </summary>
public class RopeStringConcurrencyTests
{
    private const int OperandLength = 300;

    private static readonly string LeftText = new('a', OperandLength);
    private static readonly string RightText = new('b', OperandLength);
    private static readonly string Expected = LeftText + RightText;

    /// <summary>
    /// Two threads read the same never-read node, released together, once per fresh node. The window
    /// that matters is inside the loser's walk — between its test of the memo and its reads of the
    /// operands — so rather than hope the scheduler lands a publication in it, each attempt holds one of
    /// the two threads back by a different, small amount, sweeping its start across the other's flatten.
    /// Which thread is held back alternates, so neither is always the one that publishes.
    /// </summary>
    [Test]
    public void TwoThreadsReadingANodeForTheFirstTimeBothGetItsText()
    {
        const int Attempts = 20_000;
        const int DelaySteps = 48;

        var left = new JsString(LeftText);
        var right = new JsString(RightText);
        var nodes = new JsString?[Attempts];
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i] = JsString.Concat(left, right);
        }

        nodes[0].Should().BeOfType<JsString.RopeString>("the premise: an operand pair this long must defer its copy");

        var gate = new AttemptGate();
        var outcomes = new[] { new ReaderOutcome(), new ReaderOutcome() };

        void Read(int reader)
        {
            var outcome = outcomes[reader];
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                // Loaded before arriving, so the other reader dropping the array's reference after the
                // gate cannot hand this one a null.
                var node = nodes[attempt]!;
                gate.Arrive(attempt);

                if ((attempt & 1) == reader)
                {
                    Thread.SpinWait((attempt >> 1) % DelaySteps);
                }

                outcome.Record(attempt, () =>
                {
                    var text = node.ToString();
                    return string.Equals(text, Expected, StringComparison.Ordinal) ? null : $"{text.Length} characters";
                });

                if (reader == 0)
                {
                    // Both readers passed this attempt's gate, so both hold their own reference; the
                    // array's one only keeps 20 000 flattened copies alive for nothing.
                    nodes[attempt] = null;
                }
            }
        }

        Task.WaitAll([DedicatedThread.RunAsync(() => Read(0)), DedicatedThread.RunAsync(() => Read(1))], TestBudgets.WedgeCeiling)
            .Should().BeTrue("both readers must finish; each records its failures instead of throwing, so neither leaves the other waiting at the gate");

        ReaderOutcome.AssertAllCorrect(outcomes, Attempts);
    }

    /// <summary>
    /// The same race reached the way an embedder reaches it without sharing anything: one preparation run on
    /// several engines at once. Each round prepares a fresh script, so each round's folded constants are
    /// nodes no engine has read yet, and the engines walk them in the same order at the same pace.
    /// </summary>
    [Test]
    public void EnginesRunningOnePreparedScriptAtOnceAllReadItsFoldedConcatenations()
    {
        const int EngineCount = 4;
        const int Rounds = 150;
        const int ConstantsPerScript = 64;

        var constant = $"'{LeftText}' + '{RightText}'";
        var code = $$"""
            var parts = [{{string.Join(", ", Enumerable.Repeat(constant, ConstantsPerScript))}}];
            var total = 0;
            for (var i = 0; i < parts.length; i++) { total += parts[i].charCodeAt(i); }
            total;
            """;
        const double ExpectedTotal = 'a' * ConstantsPerScript;

        AssertFoldedIntoOneSharedNode();

        var prepared = new Prepared<Script>[Rounds];
        for (var round = 0; round < Rounds; round++)
        {
            prepared[round] = Engine.PrepareScript(code);
        }

        var gate = new AttemptGate(EngineCount);
        var outcomes = Enumerable.Range(0, EngineCount).Select(_ => new ReaderOutcome()).ToArray();

        void Run(int reader)
        {
            var engine = new Engine();
            var outcome = outcomes[reader];
            for (var round = 0; round < Rounds; round++)
            {
                gate.Arrive(round);
                outcome.Record(round, () =>
                {
                    var total = engine.Evaluate(in prepared[round]).AsNumber();
                    return total == ExpectedTotal ? null : $"a total of {total}";
                });
            }
        }

        var readers = Enumerable.Range(0, EngineCount).Select(i => DedicatedThread.RunAsync(() => Run(i))).ToArray();
        Task.WaitAll(readers, TestBudgets.WedgeCeiling)
            .Should().BeTrue("every engine must finish; each records its failures instead of throwing");

        ReaderOutcome.AssertAllCorrect(outcomes, Rounds);
    }

    /// <summary>
    /// The premise of the prepared-script test: a folded long concatenation is a deferred node, and it is one
    /// instance on the preparation — two engines evaluating it are handed the same object. If preparation ever
    /// stops publishing it, or starts flattening it, that test would keep passing while covering nothing.
    /// </summary>
    private static void AssertFoldedIntoOneSharedNode()
    {
        var prepared = Engine.PrepareScript($"'{LeftText}' + '{RightText}'");

        var one = new Engine().Evaluate(in prepared);
        var other = new Engine().Evaluate(in prepared);

        one.Should().BeOfType<JsString.RopeString>();
        one.Should().BeSameAs(other);
    }

    /// <summary>
    /// Releases a fixed set of readers into each attempt together. Every reader arrives at every attempt —
    /// failures are recorded rather than thrown — so a plain counter is enough, and a reader that the
    /// scheduler parks only delays the others; it cannot strand them.
    /// </summary>
    private sealed class AttemptGate(int readers = 2)
    {
        private int _arrived;

        public void Arrive(int attempt)
        {
            var target = readers * (attempt + 1);
            Interlocked.Increment(ref _arrived);

            // A tight spin, because the release has to be close for the sweep above to mean anything; a
            // yield only once the other reader is evidently not running, so a descheduled peer costs this
            // thread nothing much on a loaded runner.
            var spins = 0;
            while (Volatile.Read(ref _arrived) < target)
            {
                if (++spins < 2_000)
                {
                    Thread.SpinWait(1);
                }
                else
                {
                    Thread.Yield();
                }
            }
        }
    }

    private sealed class ReaderOutcome
    {
        private int _failures;
        private string? _first;

        /// <param name="read">Returns <see langword="null"/> when what it read was right, and says what it read otherwise.</param>
        public void Record(int attempt, Func<string?> read)
        {
            string? failure;
            try
            {
                failure = read() is { } wrong ? $"attempt {attempt} read {wrong}" : null;
            }
            catch (Exception e)
            {
                failure = $"attempt {attempt} threw {e}";
            }

            if (failure is not null)
            {
                _failures++;
                _first ??= failure;
            }
        }

        public static void AssertAllCorrect(ReaderOutcome[] outcomes, int attempts)
        {
            var failures = outcomes.Sum(o => o._failures);
            var first = outcomes.Select(o => o._first).FirstOrDefault(f => f is not null);

            failures.Should().Be(0, $"{failures} of {attempts * outcomes.Length} reads failed; the first: {first}");
        }
    }
}
