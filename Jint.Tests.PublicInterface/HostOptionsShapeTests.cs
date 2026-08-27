#nullable enable

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The shape every option group has, seen from outside the assembly: a read-only property that materializes
/// its group on first access, and does so once however many engine builds race for it.
/// </summary>
public class HostOptionsShapeTests
{
    [Test]
    public void AGroupIsTheSameInstanceEveryTimeItIsRead()
    {
        var options = new Options();

        ReferenceEquals(options.Constraints, options.Constraints).Should().BeTrue();
        ReferenceEquals(options.Interop, options.Interop).Should().BeTrue();
        ReferenceEquals(options.Json, options.Json).Should().BeTrue();
        ReferenceEquals(options.Debugger, options.Debugger).Should().BeTrue();
        ReferenceEquals(options.Modules, options.Modules).Should().BeTrue();
    }

    [Test]
    public async Task ConcurrentReadersOfAGroupAllSeeOneInstance()
    {
        // Options is documented as safe to share between engines being constructed concurrently, and every
        // engine build reads several groups. A plain `??=` would hand two racing builds their own instance
        // each, and only one of them would see a later host mutation of the group.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var options = new Options();
            using var start = new ManualResetEventSlim(false);
            var seen = new Options.ConstraintOptions[8];

            var readers = new Task[seen.Length];
            for (var i = 0; i < readers.Length; i++)
            {
                var slot = i;
                readers[slot] = Task.Run(() =>
                {
                    start.Wait();
                    seen[slot] = options.Constraints;
                });
            }

            start.Set();
            await Task.WhenAll(readers);

            seen.Should().OnlyContain(x => ReferenceEquals(x, options.Constraints));
        }
    }

    /// <summary>
    /// The clock is the one member on <see cref="Options"/> that memoizes into a backing field on read, so
    /// the freeze resolves it: otherwise reading a frozen configuration would write to it, and threads
    /// reading it would race for which clock the engines built from it get.
    /// </summary>
    [Test]
    public async Task AFrozenConfigurationHasOneClockHoweverManyThreadsReadIt()
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var options = new Options();
            options.MakeReadOnly();

            using var start = new ManualResetEventSlim(false);
            var seen = new ITimeSystem[8];

            var readers = new Task[seen.Length];
            for (var i = 0; i < readers.Length; i++)
            {
                var slot = i;
                readers[slot] = Task.Run(() =>
                {
                    start.Wait();
                    seen[slot] = options.TimeSystem;
                });
            }

            start.Set();
            await Task.WhenAll(readers);

            seen.Should().OnlyContain(x => ReferenceEquals(x, options.TimeSystem));
        }
    }

    [Test]
    public void TheJsonGroupIsConfiguredThroughItsMembers()
    {
        // In 4.16.x Json was the only group with a public setter, which is why the untrusted-code profile's
        // private snapshot had to hand-copy it. It is read-only like every other group now.
        var options = new Options();
        options.Json.MaxParseDepth = 3;
        options.Json.MaxParseDepth.Should().Be(3);

        var engine = new Engine(options);
        engine.Evaluate("JSON.parse('{\"a\":{\"b\":1}}').a.b").AsNumber().Should().Be(1);
        Invoking(() => engine.Evaluate("JSON.parse('{\"a\":{\"b\":{\"c\":{\"d\":1}}}}')"))
            .Should().Throw<JavaScriptException>();
    }

    [Test]
    public void ASaturatedRegexTimeoutMeansUntimedRatherThanUnconstructible()
    {
        // "A limit that cannot be reached is not a limit". Regex accepts a match timeout up to int.MaxValue
        // milliseconds, so TimeSpan.MaxValue could not be one; in 4.16.x it threw out of the Regex constructor
        // on the .NET path while Jint's own engine quietly treated it as untimed.
        foreach (var timeout in new[] { TimeSpan.MaxValue, TimeSpan.Zero, TimeSpan.FromDays(1000) })
        {
            var engine = new Engine(options => options.Constraints.RegexTimeout = timeout);
            engine.Evaluate("/a(b)c/.exec('abc')[1]").AsString().Should().Be("b");
        }
    }

    [Test]
    public void AConfiguredRegexTimeoutStillReadsBackAsItWasAssigned()
    {
        // The normalization happens where the timeout is used, so a security report describes what the host
        // actually typed.
        var options = new Options();
        options.Constraints.RegexTimeout = TimeSpan.MaxValue;

        options.Constraints.RegexTimeout.Should().Be(TimeSpan.MaxValue);
        options.ValidateSecurityConfiguration().Diagnostics
            .Should().Contain(x => x.Code == SecurityDiagnosticCodes.RegexTimeoutExcessive);
    }

    /// <summary>
    /// A group materialized on one thread while another freezes the options ends up frozen either way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Options"/> is documented as safe to share between engines being constructed concurrently,
    /// so the two threads below are an ordinary embedding rather than a contrivance: one host thread finishes
    /// with the instance and publishes it, another is still touching a group. The accessor reads the owner's
    /// frozen state, allocates, and only then publishes — so a freeze landing inside those few nanoseconds
    /// sets the flag, finds the backing field still null, cascades over nothing, and the accessor publishes an
    /// <b>unfrozen</b> group onto a frozen <see cref="Options"/>. It stays writable for the life of the
    /// process, and the engine reads <c>Intl</c> and <c>Temporal</c> lazily, long after construction, so a
    /// write to one of those still lands on a live engine.
    /// </para>
    /// <para>
    /// The window cannot be held open from outside the assembly: nothing host-supplied runs between the state
    /// read and the publication, so there is no seam a <see cref="ManualResetEventSlim"/> could be wedged
    /// into. This is therefore a bounded sweep rather than a gate — two threads released from one
    /// <see cref="Barrier"/>, the materializing one walking the groups in the order <c>MakeReadOnly</c>
    /// cascades in, so that the freeze falls inside the first group's window as often as the scheduler
    /// allows. Every escape is collected rather than only the first, so a failure says how often it happened.
    /// </para>
    /// </remarks>
    [Test]
    public void AGroupMaterializedWhileTheOptionsAreBeingFrozenIsStillFrozen()
    {
        const int Attempts = 1000;

        var groups = EveryGroupInCascadeOrder();
        var escapes = new List<string>();
        var workerFailure = new StrongBox<Exception?>(null);
        Options current = null!;

        using var barrier = new Barrier(participantCount: 3);

        var materializer = StartRounds("group-materializer", barrier, Attempts, workerFailure, () =>
        {
            var options = current;
            foreach (var group in groups)
            {
                group.Read(options);
            }
        });

        var freezer = StartRounds("options-freezer", barrier, Attempts, workerFailure, () => current.MakeReadOnly());

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var options = new Options();

            // The freezing thread has to arrive at the freeze with nothing else to do first, and freezing
            // resolves the default clock. Reading it here means the round measures the publication order the
            // test is about rather than one thread's head start building a DefaultTimeSystem.
            _ = options.TimeSystem;

            current = options;

            barrier.SignalAndWait();
            barrier.SignalAndWait();

            // Collected rather than asserted, so that nothing throws while two threads are still parked on
            // the barrier this method's `using` is about to dispose.
            if (!options.IsReadOnly)
            {
                escapes.Add($"attempt {attempt}: MakeReadOnly did not take");
            }

            foreach (var group in groups)
            {
                var refusal = Caught.Exception(() => group.Write(options));
                if (refusal is null)
                {
                    escapes.Add($"attempt {attempt}: {group.Name} accepted a write on a frozen Options");
                }
                else if (refusal is not InvalidOperationException)
                {
                    escapes.Add($"attempt {attempt}: {group.Name} refused with {refusal.GetType()}");
                }
            }
        }

        materializer.Join();
        freezer.Join();

        workerFailure.Value.Should().BeNull();
        escapes.Should().BeEmpty(
            "a group materialized while MakeReadOnly runs must end up frozen, and {0} of {1} attempts published a writable one",
            escapes.Count,
            Attempts);
    }

    /// <summary>
    /// Runs <paramref name="round"/> once per attempt on a thread of its own, bracketed by the two
    /// rendezvous the caller drives.
    /// </summary>
    /// <remarks>
    /// The inner catch is what keeps a round's failure from turning into a deadlock: the thread still reaches
    /// its second <c>SignalAndWait</c>, so the other two participants are released. The outer one covers the
    /// barrier itself — an unhandled exception on either of these threads would take the whole test run down
    /// rather than fail this test.
    /// </remarks>
    private static Thread StartRounds(
        string name,
        Barrier barrier,
        int attempts,
        StrongBox<Exception?> failure,
        Action round)
    {
        var thread = new Thread(() =>
        {
            try
            {
                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    barrier.SignalAndWait();
                    try
                    {
                        round();
                    }
                    catch (Exception e)
                    {
                        Interlocked.CompareExchange(ref failure.Value, e, null);
                    }

                    barrier.SignalAndWait();
                }
            }
            catch (Exception e)
            {
                Interlocked.CompareExchange(ref failure.Value, e, null);
            }
        })
        {
            IsBackground = true,
            Name = name,
        };

        thread.Start();
        return thread;
    }

    /// <summary>
    /// Every group, in the order <c>Options.SetReadOnly</c> cascades over them, with a read that materializes
    /// it and a value setting whose refusal is the only way a host can see that it is frozen.
    /// </summary>
    private static (string Name, Action<Options> Read, Action<Options> Write)[] EveryGroupInCascadeOrder()
    {
        var groups = new List<(string, Action<Options>, Action<Options>)>
        {
            ("Options.Constraints", static o => _ = o.Constraints, static o => o.Constraints.MaxRecursionDepth = 8),
            ("Options.Parsing", static o => _ = o.Parsing, static o => o.Parsing.MaxNodeCount = 8),
            ("Options.Interop", static o => _ = o.Interop, static o => o.Interop.Enabled = true),
            ("Options.Debugger", static o => _ = o.Debugger, static o => o.Debugger.Enabled = true),
            ("Options.Coverage", static o => _ = o.Coverage, static o => o.Coverage.Enabled = true),
            ("Options.Host", static o => _ = o.Host, static o => o.Host.StringCompilationAllowed = false),
            ("Options.Modules", static o => _ = o.Modules, static o => o.Modules.RegisterRequire = true),
            ("Options.Intl", static o => _ = o.Intl, static o => o.Intl.CldrProvider = null!),
            ("Options.Temporal", static o => _ = o.Temporal, static o => o.Temporal.TimeZoneProvider = null!),
            ("Options.Json", static o => _ = o.Json, static o => o.Json.MaxParseDepth = 8),
            ("Options.Profiling", static o => _ = o.Profiling, static o => o.Profiling.Enabled = true),
        };

#if NET8_0_OR_GREATER
        groups.Add(("Options.WebApi", static o => _ = o.WebApi, static o => o.WebApi.Features = WebApiFeatures.Console));
#endif

        return groups.ToArray();
    }
}
