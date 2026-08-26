using System.Text;
using Acornima.Ast;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

// This needs to run without any parallelization because it uses
// garbage collector metrics which cannot be isolated. NUnit runs the non-parallel fixtures in a shift of
// their own, on one worker, so this runs with nothing else in flight — including the three other fixtures
// below that measure the same thing.
[NonParallelizable]
public class GarbageCollectionTests
{
    [Test]
    public void InternalCachingDoesNotPreventGarbageCollection()
    {
        // This test ensures that memory allocated within functions
        // can be garbage collected by the .NET runtime. To test that,
        // the "allocate" functions allocates a big chunk of memory,
        // which is not used anywhere. So the GC should have no problem
        // releasing that memory after the "allocate" function leaves.

        // Arrange
        var engine = new Engine();
        const string script =
            """
            function allocate(runAllocation) {
                if (runAllocation) {
                    // Allocate ~200 MB of data (not 100 because .NET uses UTF-16 for strings)
                    var test = Array.from({ length: 100 })
                        .map(() => ' '.repeat(1 * 1024 * 1024));
                }
                return 2;
            }
            """;
        engine.Evaluate(script);

        // Create a baseline for memory usage.
        engine.Evaluate("allocate(false);");
        var usedMemoryBytesBaseline = CurrentlyUsedMemory();

        // Act
        engine.Evaluate("allocate(true);");

        // Assert
        var usedMemoryBytesAfterJsScript = CurrentlyUsedMemory();
        var epsilon = 10 * 1024 * 1024; // allowing up to 10 MB of other allocations should be enough to prevent false positives
        (usedMemoryBytesAfterJsScript - usedMemoryBytesBaseline).Should().BeLessThan(epsilon, $"""
                          The garbage collector did not free the allocated but unreachable 200 MB from the script.;
                          Before Call : {BytesToString(usedMemoryBytesBaseline)}
                          After Call  : {BytesToString(usedMemoryBytesAfterJsScript)}
                          ---
                          Acceptable  : {BytesToString(usedMemoryBytesBaseline + epsilon)}
                          """);
    }

    [Test]
    public void PreparedScriptsDoNotRetainSourceTextByDefault()
    {
        // Regression test for #2560: by default a prepared script must not keep the full source text alive
        // (it was retained per function node to back Function.prototype.toString()). With many large cached
        // scripts this caused hundreds of MB of duplicated source strings. Holding N large scripts, the
        // retaining variant must keep ~N * sourceSize more bytes alive than the non-retaining default.
        //
        // Each script is a tiny function wrapped around a large, unique comment: the source string is big
        // (~commentChars * 2 bytes, UTF-16) while the AST is tiny, so the measured delta isolates the source.

        const int count = 25;
        const int commentChars = 400_000; // ~800 KB of source per script

        var retained = MeasurePreparedHeap(count, commentChars, retainSourceText: true);
        var notRetained = MeasurePreparedHeap(count, commentChars, retainSourceText: false);

        // Theoretical savings ≈ count * commentChars * 2 (UTF-16). Assert at least half to absorb noise.
        var minimumSavings = (long) count * commentChars; // bytes; conservative lower bound (< chars * 2)
        var actualSavings = retained - notRetained;
        actualSavings.Should().BeGreaterThan(minimumSavings, $"""
                          Disabling RetainFunctionSourceText did not free the expected source text.
                          Retained     : {BytesToString(retained)}
                          Not retained : {BytesToString(notRetained)}
                          Savings      : {BytesToString(actualSavings)}
                          Expected     : > {BytesToString(minimumSavings)}
                          """);

        static long MeasurePreparedHeap(int count, int commentChars, bool retainSourceText)
        {
            var options = new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { RetainFunctionSourceText = retainSourceText },
            };

            var prepared = new List<Prepared<Script>>(count);
            for (var i = 0; i < count; i++)
            {
                // The source string is built inline and never stored: only a retaining prepared script keeps
                // it reachable. With retention off it becomes collectable once parsing completes.
                prepared.Add(Engine.PrepareScript(BuildLargeScript(i, commentChars), options: options));
            }

            var used = CurrentlyUsedMemory();
            GC.KeepAlive(prepared);
            return used;
        }

        static string BuildLargeScript(int seed, int commentChars)
        {
            var sb = new StringBuilder(commentChars + 64);
            sb.Append("function big").Append(seed).Append("() {\n  /* ").Append(seed).Append(' ');
            sb.Append('x', commentChars); // unique-ish, large comment body kept only in the source text
            sb.Append(" */\n  return 0;\n}\n");
            return sb.ToString();
        }
    }

    /// <summary>
    /// The script the two shared-prepared-script retention tests run. It covers every cache shape: an ordinary
    /// function (single-slot env cache), a direct-recursive one (bounded RecursiveEnvPool), a let/const block
    /// (block env cache), a for-let loop (loop env cache), for-of/for-in with let head (per-iteration env cache on
    /// the JintForInForOfStatement handler) and a Function-constructor function (definition-level env parked on
    /// the realm-cached dynamic State, which must die with the realm).
    /// </summary>
    private const string RetentionScript = """
        function f(x) { var y = x + 1; return y; }
        function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }
        function b(x) { { let y = x + 1; const z = y * 2; f(y + z); } }
        function l(x) { var sum = 0; for (let i = 0; i < 3; i++) { sum += i; } return sum; }
        function fo(arr) { var sum = 0; for (let v of arr) { sum += v; } return sum; }
        function fi(obj) { var keys = ''; for (let k in obj) { keys += k; } return keys; }
        var dyn = new Function('a', 'return a + 1');
        f(1); f(2); fib(8); b(1); b(2); l(1); l(2);
        fo([1, 2, 3]); fo([4, 5]); fi({ a: 1, b: 2 }); fi({ c: 3 });
        dyn(1); dyn(2);
        """;

    [Test]
    public void SharedPreparedScriptDoesNotRetainEngines()
    {
        // Regression test for #2560 (secondary cause, #2413): a prepared script shared across many engines
        // must not pin those engines via environment reuse caches. Function environments are cached on the
        // ScriptFunction instance and block environments on the JintBlockStatement handler instance (both
        // per engine) rather than on state shared through the AST, so once an engine is dropped its cached
        // environments — and the engine/realm they reference — become collectable even while the prepared
        // script stays cached.

        var prepared = Engine.PrepareScript(RetentionScript);

        AssertNoEngineRetained(prepared);
    }

    [Test]
    public void SharedParseOnlyPreparedScriptDoesNotRetainEngines()
    {
        // Same claim, made where it is harder to keep: with StaticAnalysis off nothing is on the AST when the
        // first engine starts, so the function and block state the sibling test finds pre-published is instead
        // published onto the shared tree by the engines themselves, one node at a time, while they run. That is
        // exactly the shape #2560 was about — engine-owned state reaching cross-engine shared storage — so the
        // engine-neutrality of what gets published has to hold node for node, not merely by construction at
        // preparation time.

        var prepared = Engine.PrepareScript(RetentionScript, options: new ScriptPreparationOptions { StaticAnalysis = false });
        prepared.Program.ShouldCarryNothing();

        AssertNoEngineRetained(prepared);
    }

    [Test]
    public void SharedParseOnlyPreparedModuleDoesNotRetainEngines()
    {
        // The module half of the same claim: a module's bindings live in a module environment rather than the
        // global one, and its evaluation runs through the module record, but the tree it publishes onto is the
        // same shared tree.
        var prepared = Engine.PrepareModule(
            RetentionScript.Replace("var dyn", "export const dyn"),
            options: new ModulePreparationOptions { StaticAnalysis = false });

        prepared.Program.ShouldCarryNothing();

        const int count = 20;
        var references = new List<WeakReference>(count);
        for (var i = 0; i < count; i++)
        {
            references.Add(ImportOnceAndForget(prepared));
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var aliveCount = references.Count(static r => r.IsAlive);
        prepared.Program.ShouldCarryPublishedInterpreterState();

        aliveCount.Should().Be(0, $"{aliveCount} of {count} engines were not collected — the shared prepared module still pins engines.");

        // NoInlining so the engine reference cannot be stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference ImportOnceAndForget(Prepared<Module> prepared)
        {
            var engine = new Engine();
            engine.Modules.Add("main", x => x.AddModule(prepared));
            engine.Modules.Import("main");
            return new WeakReference(engine);
        }
    }

    private static void AssertNoEngineRetained(Prepared<Script> prepared)
    {
        const int count = 20;
        var references = new List<WeakReference>(count);
        for (var i = 0; i < count; i++)
        {
            // Run inside a helper so no strong reference to the engine survives on this frame.
            references.Add(RunOnceAndForget(prepared));
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var aliveCount = references.Count(static r => r.IsAlive);

        // Doubles as the keep-alive for the prepared script, and as the assertion that there was ever anything
        // shared to leak: a run that published nothing onto the tree would satisfy the collection check for the
        // wrong reason.
        prepared.Program.ShouldCarryPublishedInterpreterState();

        aliveCount.Should().Be(0, $"{aliveCount} of {count} engines were not collected — the shared prepared script still pins engines.");

        // NoInlining so the engine reference cannot be stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference RunOnceAndForget(Prepared<Script> prepared)
        {
            var engine = new Engine();
            engine.Execute(prepared);
            return new WeakReference(engine);
        }
    }

    [Test]
    public void NestedTypeAccessDoesNotRetainEngines()
    {
        // Regression test: the accessors backing static member access on a type reference live in a cache
        // shared by the whole process and keyed only by (declaring type, member name). A nested type
        // resolves to an accessor that holds a type reference — an object owned by the engine that created
        // it — so caching that accessor kept the engine, its realm and all of its intrinsics reachable for
        // the lifetime of the process. Only the engine that first resolved the member was pinned, so a
        // single engine is enough to observe it: with the leak in place this one is never collected.

        var reference = RunOnceAndForget();

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        reference.IsAlive.Should().BeFalse("the engine is still pinned by the shared static member accessor cache.");

        // NoInlining so the engine reference cannot be stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference RunOnceAndForget()
        {
            var engine = new Engine();
            engine.SetValue("Holder", TypeReference.CreateTypeReference<NestedTypeLeakHolder>(engine));
            engine.Evaluate("Holder.Inner");
            return new WeakReference(engine);
        }
    }

    [Test]
    public void ALayoutWithLazySlotsDoesNotRetainEnginesOrPerObjectState()
    {
        // A JsObjectLayout is meant to live in a static readonly field for the whole process, and one with
        // lazy slots carries delegates and is referenced from every unmaterialized slot of every object built
        // from it. If a factory, the engine's per-prototype layout memo or the sentinel reached back to the
        // engine or to the per-object state, every item of every batch ever created would be immortal.

        var layout = Jint.Native.JsObjectLayout.CreateBuilder()
            .Add("id")
            .AddLazy("value", static (_, state) => Jint.Native.JsString.Create((string) state))
            .Build();

        var references = new List<WeakReference>();
        for (var i = 0; i < 10; i++)
        {
            references.AddRange(RunOnceAndForget(layout, i));
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var aliveCount = references.Count(static r => r.IsAlive);
        GC.KeepAlive(layout);

        aliveCount.Should().Be(0, $"{aliveCount} of {references.Count} engines/objects/states were not collected — the shared layout still pins them.");

        // NoInlining so nothing stays stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference[] RunOnceAndForget(Jint.Native.JsObjectLayout layout, int i)
        {
            var engine = new Engine();
            // A distinct state object per item, and only half of the items materialized, so both the resolved
            // and the still-unmaterialized shapes are covered.
            var state = new string('x', 16 + i);
            var obj = Jint.Native.JsObject.Create(engine, layout, [Jint.Native.JsNumber.Create(i), null], state);
            engine.SetValue("o", obj);
            if (i % 2 == 0)
            {
                engine.Evaluate("o.value");
            }

            return [new WeakReference(engine), new WeakReference(state), new WeakReference(obj)];
        }
    }

    /// <summary>
    /// A warmed call site keeping its last callee is a documented, bounded cost of the two monomorphic
    /// call caches — one live callee per site, and a <c>ScriptFunction</c> drags its closure environment
    /// along. It has to stay bounded to the sites those caches can actually serve. Five arguments is more
    /// than <c>CallFromRegisters</c> has registers for and more than the built-in lane's two, so neither
    /// can ever arm here, and remembering the callee would buy a pooled engine nothing but one retained
    /// closure graph per such site, for its whole life.
    /// </summary>
    [Test]
    public void AFiveArgumentCallSiteRetainsNoCallee()
    {
        AssertCalleeRetention("f(1, 2, 3, 4, 5);", expectRetained: false);
    }

    /// <summary>
    /// The same for a spread, which makes the argument count a runtime quantity neither lane can express,
    /// regardless of how few arguments the spread happens to produce.
    /// </summary>
    [Test]
    public void ASpreadCallSiteRetainsNoCallee()
    {
        AssertCalleeRetention("f(...[1, 2]);", expectRetained: false);
    }

    /// <summary>
    /// The control, and the reason the two above can be trusted: a site the lanes <em>do</em> serve still
    /// retains its callee, which is the documented one-entry-per-site cost. Without it, a harness that
    /// simply failed to warm anything would satisfy those assertions for the wrong reason — as one written
    /// against <c>Execute(string)</c> does, since a re-parsed script gets a fresh handler tree every time
    /// and so never reaches the caches at all.
    /// </summary>
    [Test]
    public void ATwoArgumentCallSiteStillRetainsItsCallee()
    {
        AssertCalleeRetention("f(1, 2);", expectRetained: true);
    }

    private static void AssertCalleeRetention(string callSite, bool expectRetained)
    {
        var engine = new Engine();

        // A factory, so each call produces a distinct closure rather than one hoisted declaration the
        // global binding would keep alive on its own. The body reads no identifier, because a warmed
        // identifier site inside the callee caches the environment it resolved in — a separate retention,
        // of the function's own call environment and so of the function, that would mask this one.
        engine.Execute("function make() { return function (a, b, c, d, e) { return 1; }; }");

        // Prepared once and run twice: the handler-tree caches are keyed on the AST node and engage only
        // on a second evaluation of the same program on the same engine.
        var prepared = Engine.PrepareScript(callSite);

        var reference = WarmTheSiteAndForget(engine, prepared);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        reference.IsAlive.Should().Be(expectRetained, expectRetained
            ? $"`{callSite}` arms a monomorphic call cache, whose one-entry-per-site retention is by design"
            : $"`{callSite}` can arm neither call lane, so it must not remember the callee it dispatched");

        // The engine owns the handler tree under test and the prepared script owns the node it is keyed
        // on, so both have to outlive the collection.
        GC.KeepAlive(engine);
        GC.KeepAlive(prepared);

        // NoInlining so the callee cannot stay stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference WarmTheSiteAndForget(Engine engine, Prepared<Script> prepared)
        {
            engine.Execute("var f = make();");
            var reference = new WeakReference(engine.GetValue("f"));

            // Dispatching through the site is what runs the probe; the second run is what makes the
            // handler tree, and so anything it cached, outlive the evaluation.
            engine.Execute(prepared);
            engine.Execute(prepared);

            // Now nothing script-visible holds the callee, so only an interpreter cache can.
            engine.Execute("f = undefined;");
            return reference;
        }
    }

    /// <summary>
    /// Only ever used by <see cref="NestedTypeAccessDoesNotRetainEngines"/>: the accessor cache is
    /// process-wide, so a type another test also resolves would let that test's engine take the blame.
    /// </summary>
    private sealed class NestedTypeLeakHolder
    {
        public sealed class Inner
        {
        }
    }

    private static long CurrentlyUsedMemory()
    {
        // Just try to ensure that everything possible gets collected.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static string BytesToString(long bytes)
        => $"{(bytes / 1024.0 / 1024.0),6:0.0} MB";
}
