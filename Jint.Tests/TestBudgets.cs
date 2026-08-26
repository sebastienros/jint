#nullable enable

namespace Jint.Tests;

/// <summary>
/// Wall-clock budgets shared by both test suites, and the one distinction that decides which of them a
/// test may use: a budget is either part of what the test <em>asserts</em>, or it is a ceiling that exists
/// only so a wedge is reported instead of hanging the run. The two must not be sized alike, and only the
/// second kind belongs here.
/// </summary>
internal static class TestBudgets
{
    /// <summary>
    /// The promise budget for an engine whose wait is released from <em>outside</em> the engine: a module
    /// load settling from a loader's own thread, a host <c>Task</c> completing, a callback dispatched from
    /// the thread pool.
    /// <para>
    /// This is a <b>wedge ceiling, never an assertion</b>. A test using it asserts an outcome — a value, an
    /// exception type, a message — and never a duration, so a healthy run spends none of this budget and
    /// widening it can hide nothing. What it removes is the thread pool from the set of things that decide
    /// the outcome: a saturated pool injects a further worker at roughly one per 500 ms, and a
    /// continuation that misses the engine's default ten-second
    /// <c>PromiseTimeout</c> fails the test with "Timeout of 00:00:10 reached" — a symptom with nothing to
    /// do with what the test is about. Two minutes cannot be reached by a loaded runner, only by a genuine
    /// hang, and a hang reported after two minutes is still a reported failure.
    /// </para>
    /// <para>
    /// The counter-example, so the contrast is on the record: a test that asserts a budget <em>is</em>
    /// respected — <c>HostMemoryLimitTests.SynchronousImportDoesNotChargeAsyncLoaderWaitToExecutionTimeout</c>
    /// with its 200 ms execution timeout, <c>HostModuleImportStateTests</c> with its 300 ms gate,
    /// <c>AsyncModuleLoaderTests.AWarmAnswerServesTheBlockingImportEvenWhereDrainingIsImpossible</c> with
    /// its deliberately short 500 ms — states that budget itself and must never be routed through here.
    /// Ask of every use: would this test still fail if the behaviour it pins regressed? If widening the
    /// budget could mask the regression, the budget is the assertion and this constant is the wrong answer.
    /// </para>
    /// <para>
    /// The name is the suite's own: several classes already keep a private <c>WedgeCeiling</c> of exactly
    /// this length, and others the same value under a local name (<c>HandoffCeiling</c>,
    /// <c>TransportSignalCeiling</c>). This is where the reasoning lives; a file is free to go on naming it
    /// for what it bounds there, as long as it is this constant it names.
    /// </para>
    /// </summary>
    public static readonly TimeSpan WedgeCeiling = TimeSpan.FromMinutes(2);
}
