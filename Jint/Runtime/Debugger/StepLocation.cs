using System.Runtime.InteropServices;

namespace Jint.Runtime.Debugger;

/// <summary>
/// What a debugger is stopping on at a <see cref="StepLocation"/>.
/// </summary>
/// <remarks>
/// <para>
/// The three members line up with the Chrome DevTools Protocol's <c>Debugger.BreakLocation.type</c>
/// (<c>debuggerStatement</c>, <c>return</c> and the absent-type default). Jint reports no <c>call</c>
/// locations; see <see cref="DebugHandler.GetStepLocations(Program)"/>.
/// </para>
/// </remarks>
public enum StepLocationKind
{
    /// <summary>
    /// A statement, a loop test or update expression, an arrow function's expression body, or a
    /// <c>for</c>-<c>in</c>/<c>of</c> binding target.
    /// </summary>
    Statement = 0,

    /// <summary>
    /// The implicit return point at the end of a function body, where <see cref="DebugInformation.ReturnValue"/> is set.
    /// </summary>
    Return = 1,

    /// <summary>
    /// A <c>debugger</c> statement.
    /// </summary>
    DebuggerStatement = 2,
}

/// <summary>
/// One position in a program that the debugger stops at when stepping, or when a breakpoint matches it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Line"/> is 1-based and <see cref="Column"/> is 0-based, as everywhere else in Jint and
/// Acornima. The Chrome DevTools Protocol counts both from zero, so a protocol layer subtracts one
/// from <see cref="Line"/> and leaves <see cref="Column"/> alone.
/// </para>
/// <para>
/// A <see cref="BreakLocation"/> built from these three values — see <see cref="ToBreakLocation"/> —
/// is what <see cref="BreakPointCollection"/> matches, so a breakpoint set there is reached.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct StepLocation
{
    /// <summary>
    /// Initializes a step location at the given source, line and column.
    /// </summary>
    /// <param name="source">The source name the program was parsed under, or <see langword="null"/>.</param>
    /// <param name="line">The 1-based line.</param>
    /// <param name="column">The 0-based column.</param>
    /// <param name="kind">What the debugger is stopping on.</param>
    public StepLocation(string? source, int line, int column, StepLocationKind kind)
    {
        Source = source;
        Line = line;
        Column = column;
        Kind = kind;
    }

    /// <summary>
    /// Gets the source name the program was parsed under, or <see langword="null"/> when it had none.
    /// </summary>
    public string? Source { get; }

    /// <summary>
    /// Gets the 1-based line.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 0-based column.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets what the debugger is stopping on here.
    /// </summary>
    public StepLocationKind Kind { get; }

    /// <summary>
    /// Returns the break location a breakpoint at this step location is set with.
    /// </summary>
    public BreakLocation ToBreakLocation() => new(Source, Line, Column);
}
