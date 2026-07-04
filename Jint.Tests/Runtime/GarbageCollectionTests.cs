using System.Text;
using Acornima.Ast;

namespace Jint.Tests.Runtime;

// This needs to run without any parallelization because it uses
// garbage collector metrics which cannot be isolated.
[CollectionDefinition(nameof(GarbageCollectionTests), DisableParallelization = true)]
[Collection(nameof(GarbageCollectionTests))]
public class GarbageCollectionTests
{
    [Fact]
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
        Assert.True(
            usedMemoryBytesAfterJsScript - usedMemoryBytesBaseline < epsilon,
            userMessage: $"""
                          The garbage collector did not free the allocated but unreachable 200 MB from the script.;
                          Before Call : {BytesToString(usedMemoryBytesBaseline)}
                          After Call  : {BytesToString(usedMemoryBytesAfterJsScript)}
                          ---
                          Acceptable  : {BytesToString(usedMemoryBytesBaseline + epsilon)}
                          """);
    }

    [Fact]
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
        Assert.True(
            actualSavings > minimumSavings,
            userMessage: $"""
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

    [Fact]
    public void SharedPreparedScriptDoesNotRetainEngines()
    {
        // Regression test for #2560 (secondary cause, #2413): a prepared script shared across many engines
        // must not pin those engines via environment reuse caches. Function environments are cached on the
        // ScriptFunction instance and block environments on the JintBlockStatement handler instance (both
        // per engine) rather than on state shared through the AST, so once an engine is dropped its cached
        // environments — and the engine/realm they reference — become collectable even while the prepared
        // script stays cached. The script covers every cache shape: an ordinary function (single-slot env
        // cache), a direct-recursive one (bounded RecursiveEnvPool), a let/const block (block env cache)
        // and a for-let loop (loop env cache).

        var prepared = Engine.PrepareScript("""
            function f(x) { var y = x + 1; return y; }
            function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }
            function b(x) { { let y = x + 1; const z = y * 2; f(y + z); } }
            function l(x) { var sum = 0; for (let i = 0; i < 3; i++) { sum += i; } return sum; }
            f(1); f(2); fib(8); b(1); b(2); l(1); l(2);
            """);

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
        GC.KeepAlive(prepared);

        Assert.True(
            aliveCount == 0,
            userMessage: $"{aliveCount} of {count} engines were not collected — the shared prepared script still pins engines.");

        // NoInlining so the engine reference cannot be stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference RunOnceAndForget(Prepared<Script> prepared)
        {
            var engine = new Engine();
            engine.Execute(prepared);
            return new WeakReference(engine);
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
