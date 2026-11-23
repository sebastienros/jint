#nullable enable
using Jint.Runtime.RegExp.GregsEx.IR;

namespace Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

internal readonly record struct EcmaCaseMapping(int Source, int Target);

// Cold budget-admitted normalization object. Unicode generation supplies exact
// identity-omitted C/S and legacy Canonicalize mappings with provenance/digests.
// The runtime binds these spans once and implements charged direct lookup.
internal sealed class EcmaCaseTables
{
    private readonly EcmaCaseMapping[] _unicode;
    private readonly EcmaCaseMapping[] _legacy;
    internal ReadOnlySpan<EcmaCaseMapping> UnicodeFold => _unicode;
    internal ReadOnlySpan<EcmaCaseMapping> LegacyUppercase => _legacy;
    internal long RetainedBytes { get; }

    private EcmaCaseTables(ReadOnlySpan<EcmaCaseMapping> unicode, ReadOnlySpan<EcmaCaseMapping> legacy, long bytes)
    {
        _unicode = unicode.ToArray();
        _legacy = legacy.ToArray();
        RetainedBytes = bytes;
    }

    internal static bool TryCreate(ReadOnlySpan<EcmaCaseMapping> unicode,
        ReadOnlySpan<EcmaCaseMapping> legacy, ref CompileBudget budget,
        out EcmaCaseTables? tables, out RegexDiagnostic diagnostic)
    {
        tables = null;
        diagnostic = default;
        var source = new SourceRange(0, 0, RegexOffsetUnit.Utf16CodeUnits);
        var bytes = 128L + 8L * unicode.Length + 8L * legacy.Length;
        if (bytes > budget.Limits.MaxPlanBytes)
        {
            diagnostic = new(RegexDiagnosticCode.PlanLimitExceeded, 0, 0, source.Unit);
            return false;
        }
        if (!Validate(unicode, 0x10FFFF, false, ref budget, source, out diagnostic) ||
            !Validate(legacy, 0xFFFF, true, ref budget, source, out diagnostic) ||
            !budget.TryCharge((long)unicode.Length + legacy.Length, source, out diagnostic) ||
            !budget.TryReserveBytes(bytes, source, out diagnostic))
        {
            return false;
        }

        tables = new(unicode, legacy, bytes);
        return true;
    }

    private static bool Validate(ReadOnlySpan<EcmaCaseMapping> mappings, int maximum, bool legacy,
        ref CompileBudget budget, SourceRange source, out RegexDiagnostic diagnostic)
    {
        diagnostic = default;
        var previous = -1;
        foreach (var mapping in mappings)
        {
            if (!budget.TryCharge(1, source, out diagnostic))
            {
                return false;
            }

            if (mapping.Source <= previous || mapping.Source > maximum || (uint)mapping.Target > (uint)maximum ||
                mapping.Source == mapping.Target ||
                (legacy && mapping.Source >= 128 && mapping.Target < 128))
            {
                diagnostic = new(RegexDiagnosticCode.InvalidProgram, 0, 0, source.Unit);
                return false;
            }
            previous = mapping.Source;
        }
        return true;
    }
}
