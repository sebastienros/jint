using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.Runtime.Interpreter;

/// <summary>
/// A call's argument list, abstracted over where the values physically live. Two implementations
/// exist: <see cref="ArrayArguments"/> reads the <see cref="JsCallArguments"/> array the public
/// [[Call]] boundary hands over, and <see cref="RegisterArguments"/> reads locals a call site
/// evaluated straight into registers, never renting an array at all.
/// </summary>
/// <remarks>
/// Every consumer is generic and constrained <c>where TArgs : struct, IArgumentSource</c> so the JIT
/// produces one specialization per implementation and <see cref="At"/> inlines to an array index or a
/// register read. Consuming through the interface itself would box the struct and dispatch virtually,
/// which is the entire cost this abstraction exists to avoid — so never widen a parameter to
/// <c>IArgumentSource</c>.
/// </remarks>
internal interface IArgumentSource
{
    int Count { get; }

    /// <summary>
    /// The argument at <paramref name="index"/>, or <see cref="JsValue.Undefined"/> when the call
    /// supplied fewer arguments — the same answer <see cref="Arguments.At(JsValue[], int)"/> gives.
    /// </summary>
    JsValue At(int index);
}

/// <summary>
/// <see cref="IArgumentSource"/> over the argument array of an ordinary [[Call]].
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct ArrayArguments(JsCallArguments arguments) : IArgumentSource
{
    private readonly JsCallArguments _arguments = arguments;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _arguments.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue At(int index) => (uint) index < (uint) _arguments.Length ? _arguments[index] : JsValue.Undefined;
}

/// <summary>
/// <see cref="IArgumentSource"/> over up to four values held in locals, for call sites whose arity is
/// a build-time constant and which therefore never need an argument array. Registers beyond
/// <see cref="Count"/> are not read, so a caller may leave them at any value.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct RegisterArguments(JsValue a0, JsValue a1, JsValue a2, JsValue a3, int count) : IArgumentSource
{
    private readonly JsValue _a0 = a0;
    private readonly JsValue _a1 = a1;
    private readonly JsValue _a2 = a2;
    private readonly JsValue _a3 = a3;
    private readonly int _count = count;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue At(int index)
    {
        if ((uint) index >= (uint) _count)
        {
            return JsValue.Undefined;
        }

        return index switch
        {
            0 => _a0,
            1 => _a1,
            2 => _a2,
            _ => _a3,
        };
    }
}
