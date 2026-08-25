using System.Runtime.InteropServices;

namespace Jint.Runtime.Coverage;

/// <summary>
/// What a <see cref="CoverageEntry"/> counted.
/// </summary>
/// <remarks>
/// New members may be added in a future release; treat an unrecognized value as "some other executable
/// construct" rather than switching exhaustively without a default arm.
/// </remarks>
public enum CoverageEntryKind
{
    /// <summary>A statement. Blocks are never reported — see <see cref="CoverageEntry"/>.</summary>
    Statement = 0,

    /// <summary>A function body: <c>{ … }</c> for a block body, the expression for a concise arrow body.</summary>
    Function = 1,
}

/// <summary>
/// A position in a source, in the three coordinates Acornima records for every node.
/// </summary>
/// <param name="Line">1-based line.</param>
/// <param name="Column">0-based column.</param>
/// <param name="Index">0-based offset into the source text.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CoveragePosition(int Line, int Column, int Index);

/// <summary>
/// One executed construct and how many times it was entered.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Start"/> and <see cref="End"/> are the range of the construct's own AST node, so a statement
/// spans the statement and a function entry spans the function's body. Block statements are deliberately not
/// reported: the engine has several internal lanes for running a block, they differ in whether the block node
/// itself is entered, and reporting it would make the report depend on which lane ran. The statements inside
/// a block are reported either way.
/// </para>
/// <para>
/// <b>Forward-extensible.</b> This type may gain members in any release, which is why it has no public
/// constructor — a host reads one, it never builds one.
/// </para>
/// </remarks>
public sealed record CoverageEntry
{
    internal CoverageEntry(CoverageEntryKind kind, CoveragePosition start, CoveragePosition end, long hitCount)
    {
        Kind = kind;
        Start = start;
        End = end;
        HitCount = hitCount;
    }

    /// <summary>What was counted.</summary>
    public CoverageEntryKind Kind { get; }

    /// <summary>Where the construct starts.</summary>
    public CoveragePosition Start { get; }

    /// <summary>Where the construct ends.</summary>
    public CoveragePosition End { get; }

    /// <summary>
    /// How many times the construct was entered. Always greater than zero: a construct that never ran has no
    /// entry at all (see <see cref="Engine.DiagnosticOperations.GetCoverage"/> on deriving the uncovered set).
    /// </summary>
    public long HitCount { get; }
}

/// <summary>
/// The entries collected for one source, identified by the name that source was parsed under.
/// </summary>
/// <remarks>
/// <b>Forward-extensible.</b> This type may gain members in any release; it has no public constructor.
/// </remarks>
public sealed record CoverageSource
{
    internal CoverageSource(string name, IReadOnlyList<CoverageEntry> entries)
    {
        Name = name;
        Entries = entries;
    }

    /// <summary>
    /// The source name, i.e. <c>SourceLocation.SourceFile</c> of the nodes it holds: the <c>source</c>
    /// argument given to <see cref="Engine.Execute(string, string, ScriptParsingOptions)"/> and friends (<c>"&lt;anonymous&gt;"</c>
    /// when none was given), or a module's location. Never <see langword="null"/>; a source parsed without a
    /// name is reported as the empty string.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The executed constructs, ordered by <see cref="CoveragePosition.Index"/> of their start.
    /// </summary>
    public IReadOnlyList<CoverageEntry> Entries { get; }
}

/// <summary>
/// A snapshot of everything one engine has counted since it was created, or since the last
/// <see cref="Engine.DiagnosticOperations.ResetCoverage"/>.
/// </summary>
/// <remarks>
/// <b>Forward-extensible.</b> This type may gain members in any release; it has no public constructor.
/// </remarks>
public sealed record CoverageReport
{
    internal CoverageReport(IReadOnlyList<CoverageSource> sources)
    {
        Sources = sources;
    }

    /// <summary>
    /// The sources that contributed at least one executed construct, ordered by
    /// <see cref="CoverageSource.Name"/> (ordinal).
    /// </summary>
    public IReadOnlyList<CoverageSource> Sources { get; }
}
