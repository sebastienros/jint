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

    private static long CurrentlyUsedMemory()
    {
        // Just try to ensure that everything possible gets collected.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static string BytesToString(long bytes)
        => $"{(bytes / 1024.0 / 1024.0),6:0.0} MB";
}
