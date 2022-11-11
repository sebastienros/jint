using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Esprima;
using Esprima.Ast;
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
    private static readonly Completion _emptyCompletion = new(CompletionType.Normal, null!, _emptyNode);

    internal readonly SyntaxElement _source;

    public Completion(CompletionType type, JsValue value, SyntaxElement source)
    {
        Type = type;
        Value = value;
        _source = source;
    }

    public readonly CompletionType Type;
    public readonly JsValue Value;
    public ref readonly Location Location => ref _source.Location;

    public static ref readonly Completion Empty() => ref _emptyCompletion;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue GetValueOrDefault()
    {
        return Value ?? JsValue.Undefined;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAbrupt()
    {
        return Type != CompletionType.Normal;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-updateempty
    /// </summary>
    internal Completion UpdateEmpty(JsValue value)
    {
        if (Value is not null)
        {
            return this;
        }

        return new Completion(Type, value, _source);
    }
}
