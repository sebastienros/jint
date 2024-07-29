namespace Jint.Runtime.Debugger;

/// <summary>
/// Choice of handling for script <c>debugger</c> statements.
/// </summary>
public enum DebuggerStatementHandling
{
    /// <summary>
    /// No action will be taken when encountering a <c>debugger</c> statement.
    /// </summary>
    Ignore,

    /// <summary>
    /// <c>debugger</c> statements will trigger debugging through <see cref="System.Diagnostics.Debugger"/>.
    /// </summary>
    Clr,

    /// <summary>
    /// <c>debugger</c> statements will trigger a break in Jint's DebugHandler. See <see cref="DebugHandler.Break"/>.
    /// </summary>
    Script
}
