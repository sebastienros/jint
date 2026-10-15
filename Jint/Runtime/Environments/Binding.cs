using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// The binding holds a <see cref="JsString.ConcatenatedString"/> that a <c>+=</c> on it produced and nothing
    /// else holds, so the next <c>+=</c> may extend it in place rather than allocate a new value. Set only by
    /// <see cref="Binding.ChangeValueOwned"/>, cleared by every other store. A read that hands the value anywhere
    /// marks the string shared first (<see cref="Binding.Value"/>), which ends the ownership: see
    /// <see cref="JsString.ConcatenatedString"/> for why that keeps every string anyone can hold immutable.
    /// </summary>
    OwnsString = 16,
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
    /// <remarks>
    /// This is how a value leaves a binding, so it is where an owned string stops being extensible: whatever
    /// the caller does with the value, the binding's next <c>+=</c> allocates instead of writing into it. The
    /// struct is a copy here, so the flag itself stays set until the next store; the string carries the answer.
    /// </remarks>
    public JsValue Value
    {
        get
        {
            Debug.Assert(_value is not null || !IsUnboxedNumber, "unboxed binding read through Value without materialization");
            var value = _value!;
            if ((_flags & BindingFlags.OwnsString) != BindingFlags.None)
            {
                MarkShared(value);
            }

            return value;
        }
    }

    /// <summary>
    /// The value without ending an owned string's ownership. Only for a reader that cannot let a string escape:
    /// the <c>+=</c> lanes reading their own operand, the number-only slot readers, and the slot readers that
    /// test <see cref="IsPlainValue"/> first and leave every other case to <c>DeclarativeEnvironment.ReadSlotSlow</c>.
    /// Every other reader uses <see cref="Value"/>.
    /// </summary>
    public JsValue? PeekValue => _value;

    /// <summary>
    /// Whether <see cref="PeekValue"/> is the value as any reader may see it: neither an unboxed number to
    /// materialize nor an owned string to mark shared. One flags test, which the hot slot readers make in place of
    /// the null test they made before strings could be owned.
    /// </summary>
    public bool IsPlainValue => (_flags & (BindingFlags.UnboxedNumber | BindingFlags.OwnsString)) == BindingFlags.None;

    public bool CanBeDeleted => (_flags & BindingFlags.CanBeDeleted) != BindingFlags.None;
    public bool Mutable => (_flags & BindingFlags.Mutable) != BindingFlags.None;
    public bool Strict => (_flags & BindingFlags.Strict) != BindingFlags.None;

    public bool IsUnboxedNumber => (_flags & BindingFlags.UnboxedNumber) != BindingFlags.None;

    public bool OwnsString => (_flags & BindingFlags.OwnsString) != BindingFlags.None;

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
        return new Binding(argument, 0, _flags & ~(BindingFlags.UnboxedNumber | BindingFlags.OwnsString));
    }

    /// <summary>
    /// Stores a string a <c>+=</c> has just created, which nothing else holds yet, and takes ownership of it so the
    /// next <c>+=</c> can extend it in place. The caller must have created <paramref name="value"/> itself; a value
    /// that has ever been handed on refuses the claim and is stored like any other.
    /// </summary>
    public Binding ChangeValueOwned(JsString.ConcatenatedString value)
    {
        return value.TryClaimForBinding()
            ? new Binding(value, 0, (_flags & ~BindingFlags.UnboxedNumber) | BindingFlags.OwnsString)
            : ChangeValue(value);
    }

    public Binding WithUnboxedNumber(double value)
    {
        return new Binding(null, value, (_flags & ~BindingFlags.OwnsString) | BindingFlags.UnboxedNumber);
    }

    /// <summary>
    /// This binding as a copy for another storage location: an owned string is marked shared, and the copy does
    /// not own it. For code that moves <see cref="Binding"/> structs wholesale rather than reading
    /// <see cref="Value"/>.
    /// </summary>
    public Binding Detach()
    {
        if ((_flags & BindingFlags.OwnsString) == BindingFlags.None)
        {
            return this;
        }

        MarkShared(_value!);
        return new Binding(_value, _number, _flags & ~BindingFlags.OwnsString);
    }

    public bool IsInitialized() => _value is not null || (_flags & BindingFlags.UnboxedNumber) != BindingFlags.None;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MarkShared(JsValue value) => ((JsString.ConcatenatedString) value).MarkShared();
}
