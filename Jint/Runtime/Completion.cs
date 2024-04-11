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

        Type = type;
        Value = value!;
        _source = source;
    }

    public readonly CompletionType Type;
    public readonly JsValue Value;
    public readonly ref readonly SourceLocation Location => ref _source.LocationRef;

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
