// CA1822: TryGetSourceText below reads a process-wide table and touches no instance state, because the
// programs it answers for are shared — one Prepared<Script> runs on many engines and every one of them has to
// give the same answer. It stays an instance member of the Advanced facet all the same: that is where a host
// looks for engine plumbing, and a static sibling of RestoreGlobalSnapshot would be the odd one out.
#pragma warning disable CA1822

using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint;

// The source text a parse was made from, kept beside the program it produced rather than on it: Program.UserData
// belongs to the static analysis pass (CachedHoistingScope), and a second writer there would be a silent
// last-writer-wins. Weakly keyed, so an entry dies with the AST that keyed it, and engine-neutral — a string and
// nothing else — so a shared Prepared<T> keeps holding no engine.
public partial class Engine
{
    private static readonly ConditionalWeakTable<Program, string> _retainedSourceTexts = new();

    /// <summary>
    /// Remembers the string <paramref name="program"/> was parsed from. Called by <see cref="JintParser"/>,
    /// and only for a parse that retains function source text — which is what makes the reference free: that
    /// same string is already stamped onto every function node the parse produced.
    /// </summary>
    internal static void RecordSourceText(Program program, string sourceText)
    {
        // Add rather than AddOrUpdate: the key is the program this very parse just produced, so nothing else
        // can hold it yet, let alone have recorded it. AddOrUpdate is also absent from the netstandard2.0
        // asset, which would make this the one line in the file needing an #if.
        _retainedSourceTexts.Add(program, sourceText);
    }

    public sealed partial class AdvancedOperations
    {
        /// <summary>
        /// Gets the source text <paramref name="program"/> was parsed from, when that parse retained it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Retention is opt-in, through the same switch <c>Function.prototype.toString</c> uses.</b>
        /// <see cref="Options.RetainFunctionSourceText"/> covers what an engine parses itself, a module a
        /// host loader supplied included — the loader path defaults to that engine's own module parsing
        /// options. A prepared program, and a module built with parsing options a loader named itself, follow
        /// the <see cref="IParsingOptions.RetainFunctionSourceText"/> of the options they were given. Without
        /// it the answer is <see langword="false"/>; nothing is reconstructed from the AST.
        /// </para>
        /// <para>
        /// The text is the very <see cref="string"/> the host passed, not a copy of it — the whole input, so a
        /// location on any node of the program indexes into it directly.
        /// </para>
        /// <para>
        /// The program to ask about is the one <c>DebugHandler.BeforeEvaluate</c> hands over, or
        /// <see cref="Prepared{TProgram}.Program"/>. The answer does not depend on this engine: a prepared
        /// program shared by several engines answers the same on every one of them, including one that never
        /// ran it.
        /// </para>
        /// </remarks>
        /// <param name="program">The script or module AST root to look up.</param>
        /// <param name="sourceText">The retained source text, or <see langword="null"/>.</param>
        /// <returns>Whether the parse that produced <paramref name="program"/> retained its source text.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="program"/> is <c>null</c>.</exception>
        public bool TryGetSourceText(Program program, out string? sourceText)
        {
            if (program is null)
            {
                Throw.ArgumentNullException(nameof(program));
            }

            if (_retainedSourceTexts.TryGetValue(program, out var retained))
            {
                sourceText = retained;
                return true;
            }

            sourceText = null;
            return false;
        }
    }
}
