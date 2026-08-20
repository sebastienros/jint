namespace Jint.Runtime.Coverage;

/// <summary>
/// What <see cref="Options.CoverageOptions"/> asks the engine to count.
/// </summary>
/// <remarks>
/// The granularity decides what ends up in the report, not how the engine executes. Coverage is collected
/// through the same per-statement lane the debugger and the exact execution constraints use, so enabling it
/// at either granularity disarms the interpreter's tight-loop lane for the whole engine — see
/// <see cref="Engine.AdvancedOperations.GetCoverage"/> for what that means for measured code.
/// </remarks>
public enum CoverageGranularity
{
    /// <summary>
    /// Count function-body entries only. One report entry per function body that ran, spanning the body
    /// (<c>{ … }</c> for a block body, the expression for a concise arrow body), with a hit count of how
    /// many times that body was entered.
    /// </summary>
    Functions = 0,

    /// <summary>
    /// Count every executed statement as well as every function-body entry. This is the default.
    /// </summary>
    Statements = 1,
}
