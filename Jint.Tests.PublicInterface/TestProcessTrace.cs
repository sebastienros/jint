#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Xunit.v3;

[assembly: Jint.Tests.PublicInterface.TestProcessTrace]

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Writes one line to standard error as each test starts and finishes, so that a run which ends by
/// <em>losing the process</em> still says what was in flight when it went.
/// </summary>
/// <remarks>
/// <para>
/// Off unless <c>JINT_TEST_TRACE</c> is set, and it is not a substitute for a test result: xUnit already
/// reports every outcome it observes. What it covers is the one failure xUnit structurally cannot report —
/// the test host dying mid-run. When that happens the VSTest adapter waits sixty seconds for a
/// <c>TestAssemblyFinished</c> that will never arrive and then synthesises a bare
/// <c>Xunit.Sdk.TestPipelineException</c> naming no test, while the console logger prints <c>Passed!</c>
/// for the subset that did report (sebastienros/jint#3308). Standard error is chosen because it is the one
/// stream nothing in that pipeline redirects, and it is unbuffered, so the last line written survives a
/// process that never gets to flush anything.
/// </para>
/// <para>
/// Attach it to the assembly rather than to any class: the point is to cover tests nobody suspected yet.
/// The counter is what makes the trace comparable with the run summary — "482 passed" and "the 486th start"
/// identify the same moment from the two ends — and it is the count to trust: a log that merged standard
/// output into standard error can glue a trace line onto the tail of an xUnit line that did not end in a
/// newline, so counting lines undercounts and the ordinal does not.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class TestProcessTraceAttribute : BeforeAfterTestAttribute
{
    private static readonly bool Enabled =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JINT_TEST_TRACE"));

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static int _started;
    private static int _finished;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (Enabled)
        {
            Write(">>>", Interlocked.Increment(ref _started), test);
        }
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (Enabled)
        {
            Write("<<<", Interlocked.Increment(ref _finished), test);
        }
    }

    private static void Write(string marker, int ordinal, IXunitTest test)
    {
        // " :: " separates the fixed-width preamble from the display name, so that a log reader can pair
        // starts with finishes on an exact field rather than on a column count.
        Console.Error.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1,5} t={2,8:F3}s thread={3,-4} :: {4}",
            marker,
            ordinal,
            Clock.Elapsed.TotalSeconds,
            Environment.CurrentManagedThreadId,
            test.TestDisplayName));
    }
}
