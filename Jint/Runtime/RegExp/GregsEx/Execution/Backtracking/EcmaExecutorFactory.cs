#nullable enable
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;
using Jint.Runtime.RegExp.GregsEx.IR;

namespace Jint.Runtime.RegExp.GregsEx.Execution.Backtracking;

internal sealed class EcmaExecutorFactory : IEcmaExecutorFactory
{
    public bool TryCreate(EcmaProgram program, IEcmaUnicodeData unicode,
        ref CompileBudget budget, out IEcmaExecutor? executor, out RegexDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(unicode);
        executor = null;
        diagnostic = default;
        var source = new SourceRange(0, program.PatternLength, RegexOffsetUnit.Utf16CodeUnits);
        if (unicode.DataId != program.Profile.DataId)
        {
            diagnostic = new(RegexDiagnosticCode.InvalidProgram, 0, program.PatternLength, source.Unit);
            return false;
        }
        // One immutable instruction per semantic node; repeats never expand. Include the
        // retained program and wrapper/reference overhead in the complete plan ceiling.
        var bytes = 192L + 64L * program.Nodes.Length;
        if (bytes > budget.Limits.MaxPlanBytes - program.RetainedBytes)
        {
            diagnostic = new(RegexDiagnosticCode.PlanLimitExceeded, 0, program.PatternLength, source.Unit);
            return false;
        }
        var temporary = 32L + 4L * program.Nodes.Length;
        if (!budget.TryReserveBytes(temporary, source, out diagnostic))
        {
            return false;
        }

        try
        {
            var depths = new int[program.Nodes.Length];
            for (var i = 0; i < depths.Length; i++)
            {
                var n = program.Nodes[i];
                if (!budget.TryCharge(1, n.Source, out diagnostic))
                {
                    return false;
                }

                depths[i] = 1 + Math.Max(n.X < 0 ? 0 : depths[n.X], n.Y < 0 ? 0 : depths[n.Y]);
            }
            if (!budget.TryCharge(depths.Length, source, out diagnostic) ||
                !budget.TryReserveBytes(bytes, source, out diagnostic))
            {
                return false;
            }
            // Binding tables occurs only here. No Unicode provider calls in search loops.
            executor = new EcmaExecutor(program, unicode.CaseTables, program.NodeArray,
                checked(2 * depths[program.Root] + 4));
            return true;
        }
        finally { budget.ReleaseBytes(temporary); }
    }
}
