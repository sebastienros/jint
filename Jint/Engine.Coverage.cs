using Jint.Runtime;
using Jint.Runtime.Coverage;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// The engine's coverage counters, or <see langword="null"/> when
    /// <see cref="Options.CoverageOptions.Enabled"/> was not set — which is the default and therefore the
    /// state of virtually every engine.
    /// <para>
    /// This field is the entire gate. It is assigned once during construction and read once per evaluation, by
    /// <see cref="Runtime.Interpreter.EvaluationContext"/>, which folds it into the same
    /// <c>ShouldRunPerStatementChecks</c> decision debug mode and the exact constraints already make. The
    /// per-statement lane it arms is the only thing that ever calls
    /// <see cref="RunPerStatementChecks"/>, so an engine that did not ask for coverage never executes a single
    /// instruction on its behalf: the recording site is inside a method it does not call.
    /// </para>
    /// </summary>
    internal readonly CoverageCollector? _coverage;

    public partial class AdvancedOperations
    {
        /// <summary>
        /// A snapshot of what this engine has executed since it was created, or since the last
        /// <see cref="ResetCoverage"/>. Requires <see cref="Options.CoverageOptions.Enabled"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What a hit count means.</b> It is the number of times the construct's execution entry was
        /// reached, so a statement in a loop body counts once per iteration and a function body counts once
        /// per call. Two shapes count more than a caller might first expect, both for the same reason — the
        /// body is genuinely re-entered: a generator body counts once per <c>next()</c> that resumes it, and
        /// an async function body counts once per resumption after an <c>await</c>. Statements themselves are
        /// not double-counted by a resumption; execution picks up where it suspended.
        /// </para>
        /// <para>
        /// <b>Re-parsing the same source.</b> The counters are keyed on AST node identity, so a host calling
        /// <see cref="Engine.Execute(string, string, ScriptParsingOptions)"/> with the same text twice counts two different node
        /// sets for one construct. The report folds those together by source name and position, so such a host
        /// reads the total rather than one entry per parse — and a host that caches a
        /// <see cref="Prepared{TProgram}"/> (which it should) never creates the situation in the first place.
        /// The corollary is that two genuinely different sources must not share a name.
        /// </para>
        /// <para>
        /// <b>What is not in the report.</b> Only constructs that actually ran. A statement with zero hits has
        /// no entry, so the report is the covered set and not the ratio; a host that needs the uncovered set
        /// derives the denominator by walking the AST it prepared (Acornima's <c>Node.ChildNodes</c>) and
        /// subtracting. Block statements are never reported at either granularity —
        /// <see cref="CoverageEntry"/> explains why — but every statement inside a block is.
        /// </para>
        /// <para>
        /// <b>What it costs.</b> Coverage is collected through the same per-statement lane the debugger and
        /// the exact execution constraints use, so an engine collecting it runs the instrumented path: the
        /// interpreter's tight-loop lane is disarmed for that engine, exactly as registering an exact
        /// constraint or enabling the debugger disarms it. Measured code is therefore not the code an
        /// uninstrumented engine runs — the normal bargain for statement-level coverage, and the reason the
        /// option is off by default. An engine that did not enable coverage pays nothing at all: the
        /// per-statement lane it would be recorded from is not armed, and the recording site lives inside it.
        /// </para>
        /// <para>
        /// The counters are cumulative and are deliberately not touched by
        /// <see cref="RestoreGlobalSnapshot"/>: a restore reverts the global binding table, not the engine's
        /// diagnostics. Call <see cref="ResetCoverage"/> to start a fresh measurement.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Coverage collection is not enabled on this engine. Reporting an empty result would make a
        /// configuration mistake look like a script that never ran.
        /// </exception>
        public CoverageReport GetCoverage()
        {
            var coverage = _engine._coverage;
            if (coverage is null)
            {
                ThrowNotEnabled();
            }

            return coverage.BuildReport();
        }

        /// <summary>
        /// Drops every hit count, so the next <see cref="GetCoverage"/> covers only what runs from here on.
        /// Requires <see cref="Options.CoverageOptions.Enabled"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Coverage collection is not enabled on this engine.</exception>
        public void ResetCoverage()
        {
            var coverage = _engine._coverage;
            if (coverage is null)
            {
                ThrowNotEnabled();
            }

            coverage.Reset();
        }

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void ThrowNotEnabled()
        {
            Throw.InvalidOperationException(
                "Code coverage has not been collected by this engine. Set Options.Coverage.Enabled before constructing it.");
        }
    }
}
