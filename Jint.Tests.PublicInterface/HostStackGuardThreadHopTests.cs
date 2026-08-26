#nullable enable

using System.Collections.Generic;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.Json;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Options.ConstraintOptions.MaxExecutionStackCount"/> selects a lane that continues a deep call
/// chain on a fresh thread-pool thread while the calling thread blocks. These pin that the hop is invisible
/// to the host: engine ownership travels with it, so a host callback reached across it may re-enter the
/// engine, and the memory-limit segment is charged on whichever thread is actually allocating.
/// <para>
/// Every test here runs on a deliberately small stack and asserts that the hop <em>happened</em> — that some
/// engine work ran on a thread other than the one that entered. Without that assertion a machine with a
/// roomier stack would pass them all without ever crossing the boundary they are about.
/// </para>
/// </summary>
public class HostStackGuardThreadHopTests
{
    /// <summary>Smaller than the platform default, so the recursion below reliably crosses the hop.</summary>
    private const int SmallStack = 1024 * 1024;

    /// <summary>Deeper than a 1 MB stack holds, so the lane hops rather than throwing.</summary>
    private const int DeepEnoughToHop = 2000;

    /// <summary>High enough never to fire; this suite is about the hop, not about the limit.</summary>
    private const int UnreachedStackCount = 1_000_000;

    private const string RecurseThenProbe =
        "function recurse(n) { return n === 0 ? probe(n) : recurse(n - 1) + 0; }";

    [Test]
    public void AHostCallbackReachedAcrossTheHopCanReEnterTheEngine()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(options => options.Constraints.MaxExecutionStackCount = UnreachedStackCount);
                var entryThread = Environment.CurrentManagedThreadId;
                var probeThreads = new List<int>();
                var failures = new List<string>();

                engine.SetValue("probe", new Func<int, int>(depth =>
                {
                    probeThreads.Add(Environment.CurrentManagedThreadId);
                    try
                    {
                        engine.Evaluate("1 + 1");
                        JsValue.FromObject(engine, new { a = 1 });
                        new JsonParser(engine).Parse("{\"a\":1}");
                    }
                    catch (Exception exception)
                    {
                        failures.Add($"{exception.GetType().Name}: {exception.Message}");
                    }

                    return depth;
                }));

                engine.Execute(RecurseThenProbe);
                engine.Evaluate("recurse(0)");
                engine.Evaluate($"recurse({DeepEnoughToHop})");

                probeThreads.Should().HaveCount(2);
                probeThreads[0].Should().Be(entryThread, "a shallow call never leaves the entry thread");
                probeThreads[1].Should().NotBe(
                    entryThread,
                    "the deep call has to actually cross the guard's thread hop, or this test pins nothing");
                failures.Should().BeEmpty("the host is single-threaded and the engine is running its script");
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void AMemoryLimitedScriptSurvivesTheHopWithNoHostCallbackAtAll()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(options =>
                {
                    options.Constraints.MaxExecutionStackCount = UnreachedStackCount;
                    options.LimitMemory(200_000_000);
                });

                engine.Execute("function recurse(n) { return n === 0 ? 0 : recurse(n - 1) + 0; }");
                engine.Evaluate("recurse(0)").AsNumber().Should().Be(0);
                engine.Evaluate($"recurse({DeepEnoughToHop})").AsNumber().Should().Be(0);
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void TheMemoryOperationIsChargedOnBothSidesOfTheHop()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(options =>
                {
                    options.Constraints.MaxExecutionStackCount = UnreachedStackCount;
                    options.LimitMemory(200_000_000);
                });

                var entryThread = Environment.CurrentManagedThreadId;
                var probeThread = 0;
                long observed = -1;
                string? failure = null;

                engine.SetValue("probe", new Func<int, int>(depth =>
                {
                    probeThread = Environment.CurrentManagedThreadId;
                    try
                    {
                        observed = engine.Constraints.Find<MemoryLimitConstraint>()!.AllocatedBytes;
                    }
                    catch (Exception exception)
                    {
                        failure = $"{exception.GetType().Name}: {exception.Message}";
                    }

                    return depth;
                }));

                engine.Execute(RecurseThenProbe);
                engine.Evaluate($"recurse({DeepEnoughToHop})");

                probeThread.Should().NotBe(entryThread, "the probe has to run on the far side of the hop");
                failure.Should().BeNull();
                observed.Should().BePositive(
                    "the allocations the recursion made before the hop are still charged to this operation");
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void AnUnrelatedThreadIsStillRefusedWhileTheHopHoldsTheEngine()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(options => options.Constraints.MaxExecutionStackCount = UnreachedStackCount);
                var entryThread = Environment.CurrentManagedThreadId;
                var probeThread = 0;
                string? outcome = null;

                using var insideTheHop = new ManualResetEventSlim(false);
                using var unrelatedThreadAnswered = new ManualResetEventSlim(false);

                engine.SetValue("probe", new Func<int, int>(depth =>
                {
                    probeThread = Environment.CurrentManagedThreadId;
                    insideTheHop.Set();
                    unrelatedThreadAnswered.Wait(TestBudgets.WedgeCeiling);
                    return depth;
                }));

                var unrelated = new Thread(() =>
                {
                    insideTheHop.Wait(TestBudgets.WedgeCeiling);
                    try
                    {
                        engine.Evaluate("1 + 1");
                        outcome = "admitted";
                    }
                    catch (InvalidOperationException)
                    {
                        outcome = "refused";
                    }
                    catch (Exception exception)
                    {
                        outcome = exception.GetType().Name;
                    }

                    unrelatedThreadAnswered.Set();
                })
                {
                    IsBackground = true,
                };

                unrelated.Start();
                engine.Execute(RecurseThenProbe);
                engine.Evaluate($"recurse({DeepEnoughToHop})");
                unrelated.Join(TestBudgets.WedgeCeiling);

                probeThread.Should().NotBe(entryThread, "the probe has to run on the far side of the hop");
                outcome.Should().Be(
                    "refused",
                    "the hop transfers ownership rather than releasing it, so the engine is still in use");
            },
            maxStackSize: SmallStack);
    }
}
