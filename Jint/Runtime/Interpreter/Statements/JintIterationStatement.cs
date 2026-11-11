using System.Runtime.CompilerServices;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// An iteration statement and the label set LabelledEvaluation evaluates it with: every label stacked directly on it,
/// so <c>a: b: for (...)</c> is evaluated with {a, b}.
/// https://tc39.es/ecma262/#sec-runtime-semantics-labelledevaluation
/// </summary>
internal abstract class JintIterationStatement<T> : JintStatement<T> where T : Statement
{
    // The labels stacked above the innermost one, which Acornima records on the loop itself (Statement.LabelSet). The
    // outermost JintLabeledStatement of the stack fills this in once, as it is built, and only for two or more labels,
    // so a loop with no label or one is left exactly as it was.
    private string[]? _enclosingLabels;

    protected JintIterationStatement(T statement) : base(statement)
    {
    }

    internal sealed override void SetEnclosingLabels(string[] labels) => _enclosingLabels = labels;

    /// <summary>
    /// LoopContinues steps 4 and 5 for a <c>continue</c> that names a label: whether this loop's label set holds it.
    /// Out of line, because only a labelled <c>continue</c> reaches it and a loop's per-iteration path must not grow.
    /// https://tc39.es/ecma262/#sec-loopcontinues
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected bool LabelSetContains(string target)
    {
        if (string.Equals(target, _statement.LabelSet?.Name, StringComparison.Ordinal))
        {
            return true;
        }

        var enclosing = _enclosingLabels;
        if (enclosing is not null)
        {
            foreach (var label in enclosing)
            {
                if (string.Equals(target, label, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
