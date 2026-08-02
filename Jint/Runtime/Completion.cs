using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.Runtime;

public enum CompletionType : byte
{
    Normal = 0,
    Return = 1,
    Throw = 2,
    Break,
    Continue
}

/// <summary>
/// https://tc39.es/ecma262/#sec-completion-record-specification-type
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct Completion
{
    private static readonly Node _emptyNode = new Identifier("");
    private static readonly Completion _emptyCompletion = new(CompletionType.Normal, JsEmpty.Instance, _emptyNode);

    internal readonly Node _source;

    public Completion(CompletionType type, JsValue value, Node source)
    {
        Debug.Assert(value is not null);

        // Target reads the label back out of the source node, so a Break/Continue completion must
        // carry the jump statement that produced it. In-box invariant: the only two producers are
        // JintBreakStatement and JintContinueStatement, which pass their own _statement.
        Debug.Assert(type != CompletionType.Break || source is BreakStatement);
        Debug.Assert(type != CompletionType.Continue || source is ContinueStatement);

        Type = type;
        Value = value!;
        _source = source;
    }

    public readonly CompletionType Type;
    public readonly JsValue Value;
    public readonly ref readonly SourceLocation Location => ref _source.LocationRef;

    /// <summary>
    /// The spec's Completion Record [[Target]] — the label a <c>break</c>/<c>continue</c> names, or
    /// null for an unlabelled jump. Recovered from <see cref="_source"/> rather than stored, so it
    /// costs no space in the struct and, unlike the mutable per-context slot it replaces, cannot be
    /// clobbered by code that runs while the completion is unwinding (a non-empty <c>finally</c>, an
    /// iterator's <c>return()</c>, a <c>Symbol.dispose</c> body). Only meaningful for Break and
    /// Continue; every read site is already guarded on <see cref="Type"/>.
    /// </summary>
    internal string? Target => Type switch
    {
        CompletionType.Break => ((BreakStatement) _source).Label?.Name,
        CompletionType.Continue => ((ContinueStatement) _source).Label?.Name,
        _ => null,
    };

    public static ref readonly Completion Empty() => ref _emptyCompletion;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue GetValueOrDefault() => Value.IsEmpty ? JsValue.Undefined : Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAbrupt() => Type != CompletionType.Normal;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-updateempty
    /// </summary>
    internal Completion UpdateEmpty(JsValue value)
    {
        if (Value?._type != InternalTypes.Empty)
        {
            return this;
        }

        return new Completion(Type, value, _source);
    }
}
