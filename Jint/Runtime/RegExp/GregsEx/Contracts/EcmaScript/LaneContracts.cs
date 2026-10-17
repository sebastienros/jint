#nullable enable
using Jint.Runtime.RegExp.GregsEx.IR;

namespace Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

// A separate ABI: RegularCore scalar semantics are not extended or reinterpreted.
internal enum EcmaNodeKind
{
    Empty, Literal, Set, Sequence, Alternative, Repeat, Capture, Backreference,
    Start, End, WordBoundary, NotWordBoundary, Lookahead, NegativeLookahead,
    Lookbehind, NegativeLookbehind
}

// X/Y are bottom-up child IDs, -1 if unused. Effective flags contain only i/m/s.
// Value is literal, set index, capture ID or numeric backreference ID. Named
// backreferences use NameIndex instead of Value. Repeat Max=-1 means unbounded.
// FirstCapture/CaptureCount identify captures contained by this node, including
// this node if it is Capture. Repeat clears its child's entire capture range.
internal readonly record struct EcmaNode(EcmaNodeKind Kind, int X, int Y, int Value,
    int NameIndex, int Min, int Max, bool Lazy, EcmaScriptFlags EffectiveFlags,
    int FirstCapture, int CaptureCount, SourceRange Source);

internal readonly record struct EcmaInterval(int First, int Last);
internal readonly record struct EcmaStringSlice(int Offset, int Length);
internal readonly record struct EcmaSetSlice(int IntervalOffset, int IntervalCount,
    int StringOffset, int StringCount);
internal readonly record struct EcmaNameSlice(string Name, int Offset, int Length);

internal enum EcmaPropertyKind { GeneralCategory, Script, ScriptExtensions, Binary, String }
internal readonly record struct EcmaProperty(EcmaPropertyKind Kind, string CanonicalName);

// Parser-owned pinned tables: no borrowed caller storage and no host categories.
// Views refer to immutable static data. Unknown aliases return false, no loose matching.
internal interface IEcmaUnicodeData
{
    string DataId { get; }
    bool TryResolveProperty(ReadOnlySpan<char> name, ReadOnlySpan<char> value,
        bool unicodeSets, out EcmaProperty property);
    ReadOnlySpan<EcmaInterval> GetIntervals(EcmaProperty property);
    ReadOnlySpan<EcmaStringSlice> GetStrings(EcmaProperty property);
    ReadOnlySpan<int> GetStringCodePoints(EcmaProperty property);
    // Bound once at executor construction. VM loops use direct immutable spans,
    // never an interface call per literal/backreference/boundary character.
    EcmaCaseTables CaseTables { get; }
    bool IsIdentifierStart(int codePoint);
    bool IsIdentifierContinue(int codePoint);
}

internal interface IEcmaCompiler
{
    // Pattern/source offsets are UTF-16; integration rejects byte options first.
    bool TryCompile(ReadOnlySpan<char> pattern, EcmaScriptProfile profile,
        IEcmaUnicodeData unicode, ref CompileBudget budget, out EcmaProgram? program,
        out EcmaScriptDiagnostic diagnostic);
}

internal enum EcmaStartPolicy { LowerBound, BuiltinLastIndex }

internal interface IEcmaExecutor
{
    EcmaProgram Program { get; }
    // frameCapacity is a finite positive configured reserve, not input-sized storage.
    // Runtime owns exact layouts; all frames/rollback/repeats live in these partitions.
    RegexWorkspaceCapacity GetRequiredCapacity(SearchOperation operation, int frameCapacity);
    RegexSearchStatus Search(ReadOnlySpan<char> input, int startAt, bool sticky,
        EcmaStartPolicy startPolicy, SearchOperation operation, ref RunState state,
        Span<RegexCapture> groups, out RegexMatchRange match);
}

internal interface IEcmaExecutorFactory
{
    // VM lowering also charges the same compile budget before publishing.
    bool TryCreate(EcmaProgram program, IEcmaUnicodeData unicode,
        ref CompileBudget budget, out IEcmaExecutor? executor, out RegexDiagnostic diagnostic);
}
