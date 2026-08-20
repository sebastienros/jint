#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Where a specification algorithm suspends on an <c>Await</c> and where it merely carries on is observable
/// from script, because every job boundary lets an unrelated promise chain run a turn. These tests pin the
/// boundaries of the two algorithms that used to re-enqueue their own continuation through a raw event-loop
/// job — <c>Array.fromAsync</c> and a <c>for await</c> loop inside an async generator — against a competing
/// chain of plain <c>.then</c> turns.
/// </summary>
/// <remarks>
/// <para>
/// The numbers below are change detectors, not a conformance claim. <c>Array.fromAsync</c> still awaits the
/// iterator's value <em>and</em> the mapped value where
/// https://tc39.es/proposal-array-from-async/#sec-array.fromAsync awaits only the mapped one, so its turn
/// count is not yet the specification's. What the algorithm no longer does is spend a job on re-entering its
/// own loop, which is what every figure here got smaller by.
/// </para>
/// </remarks>
public class AlgorithmJobBoundaryTests
{
    /// <summary>
    /// Runs <paramref name="script"/> and a 30-turn promise chain in the same job, and returns the log both
    /// wrote to. Same job matters: <c>Execute</c> drains the event loop before it returns, so a chain built
    /// by a second <c>Execute</c> would start after the algorithm had already finished.
    /// </summary>
    private static string Interleave(string script)
    {
        var engine = new Engine();
        engine.Execute(
            "var log = [];"
            + script
            + "var p = Promise.resolve(); for (let i = 0; i < 30; i++) { const j = i; p = p.then(() => log.push('t' + j)); }");

        // Execute's own drain runs the loop to exhaustion; the extra pumps only guard against a future
        // change that leaves something queued behind it.
        for (var i = 0; i < 5; i++)
        {
            engine.Advanced.ProcessTasks();
        }

        return engine.Evaluate("log.join(' ')").AsString();
    }

    /// <summary>How many turns of the competing promise chain ran before <paramref name="marker"/> was logged.</summary>
    private static int TurnsBefore(string log, string marker)
    {
        var entries = log.Split(' ');
        var index = Array.IndexOf(entries, marker);
        index.Should().BeGreaterThanOrEqualTo(0, "'{0}' must appear in the log, which was: {1}", marker, log);

        var turns = 0;
        for (var i = 0; i < index; i++)
        {
            if (entries[i].Length > 1 && entries[i][0] == 't')
            {
                turns++;
            }
        }

        return turns;
    }

    [Fact]
    public void ArrayFromAsyncOverASyncIterableSpendsNoJobOnReEnteringItsLoop()
    {
        // Was 14: the non-mapping path re-enqueued twice per element, once to store the value and once to
        // re-enter the loop, so three elements cost six jobs the algorithm never asks for.
        var log = Interleave("Array.fromAsync([1, 2, 3]).then(a => log.push('done:' + a.join('')));");
        TurnsBefore(log, "done:123").Should().Be(8);
    }

    [Fact]
    public void ArrayFromAsyncOverAnArrayLikeSpendsNoJobOnReEnteringItsLoop()
    {
        // Was 6: one re-enqueue per element.
        var log = Interleave("Array.fromAsync({ length: 3, 0: 1, 1: 2, 2: 3 }).then(a => log.push('done:' + a.join('')));");
        TurnsBefore(log, "done:123").Should().Be(3);
    }

    [Fact]
    public void ArrayFromAsyncWithAMapperSpendsNoJobOnReEnteringItsLoop()
    {
        // Was 14: two re-enqueues per element.
        var log = Interleave("Array.fromAsync([1, 2], x => x * 2).then(a => log.push('done:' + a.join('')));");
        TurnsBefore(log, "done:24").Should().Be(10);
    }

    /// <summary>
    /// The re-enqueue the loop used to make was commented "Queue next iteration to prevent stack overflow".
    /// It was not what prevented one: every turn of the loop ends in an <c>Await</c>, and
    /// <c>PerformPromiseThen</c> queues its reaction rather than calling it, so the turn returns before the
    /// next one starts however many elements there are. Twenty thousand of them is far more than a nested
    /// frame per element would survive.
    /// </summary>
    [Theory]
    [InlineData("arrayLike")]
    [InlineData("array")]
    [InlineData("arrayLike, x => x + 1")]
    public void ArrayFromAsyncDoesNotRecurPerElement(string arguments)
    {
        var engine = new Engine();
        engine.Execute("""
            const n = 20000;
            const arrayLike = { length: n };
            const array = [];
            for (let i = 0; i < n; i++) { arrayLike[i] = i; array.push(i); }
            var outcome = 'pending';
            """);
        engine.Execute($"Array.fromAsync({arguments}).then(a => {{ outcome = a.length + '/' + a[a.length - 1]; }}, e => {{ outcome = 'rejected:' + e; }});");

        for (var i = 0; i < 5; i++)
        {
            engine.Advanced.ProcessTasks();
        }

        var expectedLast = arguments.Contains("x + 1", StringComparison.Ordinal) ? 20000 : 19999;
        engine.Evaluate("outcome").AsString().Should().Be("20000/" + expectedLast);
    }

    [Fact]
    public void ForAwaitInsideAnAsyncGeneratorResumesInsideItsOwnReactionJob()
    {
        // https://tc39.es/ecma262/#await: the fulfilled closure pushes the suspended context back on and
        // resumes it there and then, so an Await costs the reaction job and nothing more. The async-generator
        // branch used to hop once more, which cost one extra turn per loop iteration — 4 / 9 / 13 before.
        var log = Interleave("""
            async function* g() { for await (const v of [1, 2]) { yield v; } }
            (async () => { for await (const v of g()) { log.push('g' + v); } log.push('gdone'); })();
            """);

        TurnsBefore(log, "g1").Should().Be(3);
        TurnsBefore(log, "g2").Should().Be(7);
        TurnsBefore(log, "gdone").Should().Be(10);
    }
}
