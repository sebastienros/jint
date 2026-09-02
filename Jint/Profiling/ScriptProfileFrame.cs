using System.Runtime.InteropServices;

namespace Jint.Profiling;

/// <summary>
/// A function as it appears in a profile: the name to show, and where its source is.
/// </summary>
/// <param name="Name">
/// The function's name at the moment the profiler first saw it, or <c>&lt;anonymous&gt;</c> when it has
/// none. Never <see langword="null"/> and never empty.
/// </param>
/// <param name="File">
/// The source file the function was parsed from — the <c>source</c> argument of the <c>Execute</c> /
/// <c>Evaluate</c> / <c>PrepareScript</c> call, or a module's location — or <see langword="null"/> for a
/// function with no source position (every built-in, and every host CLR callable).
/// </param>
/// <param name="Line">One-based line of the function's declaration, or <see langword="null"/> with <see cref="File"/>.</param>
/// <param name="Column">One-based column of the function's declaration, or <see langword="null"/> with <see cref="File"/>.</param>
/// <param name="Program">
/// The abstract syntax tree the function was parsed as part of — the reference
/// <c>DebugHandler.BeforeEvaluate</c> hands over — or <see langword="null"/> for a function with no source,
/// and for one the engine reached through <c>eval</c> or the <c>Function</c> constructor.
/// </param>
/// <remarks>
/// <para>
/// Frames are interned by <em>definition</em>, not by function object: every closure instantiated from one
/// source function is one frame, which is what makes a profile of a closure-heavy program readable. Functions
/// with no definition — built-ins, bound functions, host callables — are interned per object instead, there
/// being nothing else to share.
/// </para>
/// <para>
/// <see cref="Column"/> is one-based to match the column Jint reports in a stack trace, where the underlying
/// parser position is a zero-based index.
/// </para>
/// <para>
/// <see cref="Program"/> identifies the script two frames share where <see cref="File"/> only names it, so
/// several programs parsed under one name — every <c>Execute</c> given no source is <c>&lt;anonymous&gt;</c>
/// — stay apart. Hold it as an identity: the tree itself is the engine's, and reading it from another thread
/// while that engine runs is not supported.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ScriptProfileFrame(string Name, string? File, int? Line, int? Column, Program? Program = null);
