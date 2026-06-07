using System.Diagnostics;
using Jint.Native;

namespace Jint.Runtime.Environments;

[Flags]
internal enum BindingFlags : byte
{
    None = 0,
    CanBeDeleted = 1,
    Mutable = 2,
    Strict = 4,

    /// <summary>
    /// The binding holds a raw number in <see cref="Binding._number"/> instead of a materialized
    /// <see cref="JsValue"/>. Only ever set for slot-stored bindings by the numeric
    /// read-modify-write fast paths; a materializing read converts back (with write-back caching).
    /// </summary>
    UnboxedNumber = 8,
}

[DebuggerDisplay("Mutable: {Mutable}, Strict: {Strict}, CanBeDeleted: {CanBeDeleted}, Value: {_value}, Unboxed: {IsUnboxedNumber} ({_number})")]
internal readonly struct Binding
{
    private readonly JsValue? _value;
    private readonly double _number;
    private readonly BindingFlags _flags;

    public Binding(
        JsValue value,
        bool canBeDeleted,
        bool mutable,
        bool strict)
    {
        _value = value;
        _number = 0;
        _flags = (canBeDeleted ? BindingFlags.CanBeDeleted : BindingFlags.None)
                 | (mutable ? BindingFlags.Mutable : BindingFlags.None)
                 | (strict ? BindingFlags.Strict : BindingFlags.None);
    }

    private Binding(JsValue? value, double number, BindingFlags flags)
    {
        _value = value;
        _number = number;
        _flags = flags;
    }

    /// <summary>
    /// The materialized value; readers must check <see cref="HasReferenceValue"/> (or use the
    /// environment's materializing accessors) so unboxed bindings are not observed as null.
    /// </summary>
    public JsValue Value
    {
        get
        {
            Debug.Assert(_value is not null || !IsUnboxedNumber, "unboxed binding read through Value without materialization");
            return _value!;
        }
    }

    public bool CanBeDeleted => (_flags & BindingFlags.CanBeDeleted) != BindingFlags.None;
    public bool Mutable => (_flags & BindingFlags.Mutable) != BindingFlags.None;
    public bool Strict => (_flags & BindingFlags.Strict) != BindingFlags.None;

    public bool IsUnboxedNumber => (_flags & BindingFlags.UnboxedNumber) != BindingFlags.None;

    public bool HasReferenceValue => _value is not null;

    public double UnboxedNumber
    {
        get
        {
            Debug.Assert(IsUnboxedNumber);
            return _number;
        }
    }

    public Binding ChangeValue(JsValue argument)
    {
        return new Binding(argument, 0, _flags & ~BindingFlags.UnboxedNumber);
    }

    public Binding WithUnboxedNumber(double value)
    {
        return new Binding(null, value, _flags | BindingFlags.UnboxedNumber);
    }

    public bool IsInitialized() => _value is not null || (_flags & BindingFlags.UnboxedNumber) != BindingFlags.None;
}
