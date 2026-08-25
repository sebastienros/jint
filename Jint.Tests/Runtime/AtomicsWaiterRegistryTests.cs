#nullable enable
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Atomics;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The registry every <c>Atomics.wait</c> and <c>Atomics.waitAsync</c> registers in used to be a process-wide
/// static keyed on the shared data block, and nothing ever removed anything from it. An async waiter holds its
/// engine — that is how it resolves its promise back onto the right event loop — so a wait nobody ever notifies
/// kept that engine, its realm and everything they root alive for the life of the process. A wait that
/// <i>ended</i> was no better off at the other end: its entry key held the shared data block itself forever.
/// </summary>
// Shares the garbage-collection collection: these tests read GC state, which cannot be isolated from tests
// running in parallel with them.
[Collection(nameof(GarbageCollectionTests))]
public class AtomicsWaiterRegistryTests
{
    [Fact]
    public void ADefaultAgentCannotSuspendAndDoesNotRegisterAnIndefiniteWaiter()
    {
        var options = new Options();
        options.AgentCanSuspend.Should().BeFalse();

        var engine = new Engine(options);
        engine.Execute("var sab = new SharedArrayBuffer(8); var i32a = new Int32Array(sab);");
        var block = BlockOf(engine, "sab");
        Exception? error = null;

        var thread = new Thread(() => error = Record.Exception(() => engine.Evaluate("Atomics.wait(i32a, 0, 0);")))
        {
            IsBackground = true
        };
        thread.Start();

        var completedWithoutBlocking = thread.Join(TimeSpan.FromSeconds(2));
        if (!completedWithoutBlocking)
        {
            var notifier = new Engine();
            notifier.SetValue("i32a", SharedView(notifier, block));

            // Wait until a regressed implementation has registered its waiter before notifying it. A single
            // notification can race the worker and leave an indefinite background wait behind in the process.
            var cleanupDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (thread.IsAlive && DateTime.UtcNow < cleanupDeadline)
            {
                if (AtomicsInstance.WaiterListCount(block) != 0)
                {
                    notifier.Evaluate("Atomics.notify(i32a, 0)");
                }

                thread.Join(TimeSpan.FromMilliseconds(10));
            }

            thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
        }

        completedWithoutBlocking.Should().BeTrue("the default agent must reject an indefinite wait instead of blocking");
        var exception = error.Should().BeOfType<JavaScriptException>().Which;
        exception.Error.InstanceofOperator(engine.Intrinsics.TypeError).Should().BeTrue();
        exception.Message.Should().Be("Atomics.wait cannot be used in this agent");
        AtomicsInstance.WaiterListCount(block).Should().Be(0, "a rejected wait must not register a waiter");
    }

    [Fact]
    public void AWorkerLikeAgentCanOptIntoSynchronousWait()
    {
        var engine = new Engine(static options => options.AgentCanSuspend = true);

        engine.Evaluate("""
            var i32a = new Int32Array(new SharedArrayBuffer(8));
            Atomics.wait(i32a, 0, 0, 0);
            """).AsString().Should().Be("timed-out");
    }

    [Fact]
    public void WaitAsyncRemainsAvailableToTheDefaultAgent()
    {
        var engine = new Engine();

        engine.Evaluate("""
            var i32a = new Int32Array(new SharedArrayBuffer(8));
            var result = Atomics.waitAsync(i32a, 0, 1);
            result.async + ":" + result.value;
            """).AsString().Should().Be("false:not-equal");
    }

    [Fact]
    public void AWaitAsyncNobodyNotifiesDoesNotKeepItsEngineAlive()
    {
        // The three lines #3025 reports. The wait asks for no timeout, so it never resolves and nothing ever
        // removes it from the list it was added to.
        var reference = WaitAsyncForeverAndForget();

        Collect();

        reference.IsAlive.Should().BeFalse("an Atomics.waitAsync nobody can ever notify must not outlive its engine");

        // NoInlining so the engine reference cannot be stack-rooted in this frame across the GC.Collect calls.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference WaitAsyncForeverAndForget()
        {
            var engine = new Engine();
            engine.Execute("""
                var i32a = new Int32Array(new SharedArrayBuffer(8));
                Atomics.waitAsync(i32a, 0, 0);
                """);
            return new WeakReference(engine);
        }
    }

    [Fact]
    public void AWaitThatEndedDoesNotKeepItsSharedDataBlockAlive()
    {
        // The registry key is the shared data block itself, so an entry left behind pins the whole block —
        // eight bytes here, but the script chooses the size. A wait that timed out immediately is enough to
        // create the entry: the waiter is added to the list before the zero timeout is examined.
        var reference = WaitAndForgetTheBlock();

        Collect();

        reference.IsAlive.Should().BeFalse("a completed Atomics.wait must not pin the SharedArrayBuffer's data block");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference WaitAndForgetTheBlock()
        {
            var engine = new Engine(static options => options.AgentCanSuspend = true);
            engine.Execute("""
                var sab = new SharedArrayBuffer(8);
                var i32a = new Int32Array(sab);
                Atomics.wait(i32a, 0, 0, 0);
                """);
            return new WeakReference(BlockOf(engine, "sab"));
        }
    }

    [Fact]
    public void AWaiterStaysVisibleToAnotherAgentThatCanStillReachTheSharedBlock()
    {
        // The deliberate half of the lifetime decision. A waiter belongs to the shared data block, not to the
        // reachability of the agent that registered it: as long as any agent can still reach the block it can
        // still call Atomics.notify, and the entry — and the engine it resolves into — has to survive to be
        // woken. Only when the block itself is unreachable can nobody notify it again, which is when the tests
        // above expect the entry to go.
        var owner = new Engine();
        owner.Execute("var sab = new SharedArrayBuffer(8); var i32a = new Int32Array(sab);");
        var block = BlockOf(owner, "sab");

        var reference = WaitAsyncForeverInASecondAgentAndForget(block);

        Collect();

        reference.IsAlive.Should().BeTrue("an agent that can still be notified must not be collected");
        owner.Evaluate("Atomics.notify(i32a, 0)").AsNumber().Should().Be(1, "the other agent's waiter must still be there to wake");

        GC.KeepAlive(block);

        // NoInlining so the second engine cannot be stack-rooted in this frame across the GC.Collect calls.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference WaitAsyncForeverInASecondAgentAndForget(byte[] block)
        {
            var engine = new Engine();
            engine.SetValue("i32a", SharedView(engine, block));
            engine.Execute("Atomics.waitAsync(i32a, 0, 0);");
            return new WeakReference(engine);
        }
    }

    [Fact]
    public void AWaitRegisteredAfterAnEarlierOneEndedIsStillNotified()
    {
        // Removing the emptied list is only safe if the next wait on the same index builds a new one and the
        // notify that follows finds it. Nothing observable may depend on whether a previous wait pruned.
        var engine = new Engine(static options => options.AgentCanSuspend = true);
        engine.Execute("""
            var i32a = new Int32Array(new SharedArrayBuffer(8));
            Atomics.wait(i32a, 0, 0, 0);
            var outcome = 'pending';
            Atomics.waitAsync(i32a, 0, 0, 180000).value.then(function (v) { outcome = v; });
            var notified = Atomics.notify(i32a, 0);
            """);

        engine.Evaluate("notified").AsNumber().Should().Be(1);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline && engine.Evaluate("outcome").AsString() == "pending")
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(5);
        }

        engine.Evaluate("outcome").AsString().Should().Be("ok");
    }

    [Fact]
    public void AWaiterListThatHasEmptiedIsRemoved()
    {
        // Every index ever waited on used to leave its own list behind, for as long as the block lived. The
        // block below stays reachable throughout, so nothing but the pruning can bring the count back down.
        var engine = new Engine(static options => options.AgentCanSuspend = true);
        engine.Execute("var sab = new SharedArrayBuffer(4096); var i32a = new Int32Array(sab);");
        var block = BlockOf(engine, "sab");

        AtomicsInstance.WaiterListCount(block).Should().Be(0);

        engine.Execute("for (var i = 0; i < i32a.length; i++) { Atomics.wait(i32a, i, 0, 0); }");

        AtomicsInstance.WaiterListCount(block).Should().Be(0, "a wait that has ended leaves nobody in its list");

        // A waiter that is still waiting of course keeps its list, and gives it up when it is woken.
        engine.Execute("Atomics.waitAsync(i32a, 0, 0);");
        AtomicsInstance.WaiterListCount(block).Should().Be(1);

        engine.Evaluate("Atomics.notify(i32a, 0)").AsNumber().Should().Be(1);
        AtomicsInstance.WaiterListCount(block).Should().Be(0, "a notified waiter leaves its list empty too");

        GC.KeepAlive(block);
    }

    private static byte[] BlockOf(Engine engine, string name)
    {
        return ((JsArrayBuffer) engine.Evaluate(name))._arrayBufferData!;
    }

    /// <summary>
    /// A SharedArrayBuffer in <paramref name="engine"/> viewing a data block another engine allocated, which is
    /// what makes two engines one agent cluster. It is how <c>Test262AgentManager</c> hands a broadcast buffer
    /// to an agent, and the only way to build one: <see cref="JsSharedArrayBuffer"/> is internal, so nothing an
    /// embedder can call creates a view over a block another engine allocated.
    /// </summary>
    private static JsValue SharedView(Engine engine, byte[] block)
    {
        var prototype = (ObjectInstance) engine.Realm.Intrinsics.SharedArrayBuffer.Get(CommonProperties.Prototype);
        var sab = new JsSharedArrayBuffer(engine, block, null, (uint) block.Length) { _prototype = prototype };
        return engine.Realm.Intrinsics.Int32Array.Construct([sab], engine.Realm.Intrinsics.Int32Array);
    }

    private static void Collect()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }
}
