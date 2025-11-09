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
        return;

        static string BytesToString(long bytes)
            => $"{(bytes / 1024.0 / 1024.0),6:0.0} MB";

        static long CurrentlyUsedMemory()
        {
            // Just try to ensure that everything possible gets collected.
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            var currentlyUsed = GC.GetTotalMemory(forceFullCollection: true);
            return currentlyUsed;
        }
    }
}
