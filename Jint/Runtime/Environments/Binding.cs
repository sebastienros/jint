using System.Diagnostics;
using Jint.Native;

namespace Jint.Runtime.Environments;

[DebuggerDisplay("Mutable: {Mutable}, Strict: {Strict}, CanBeDeleted: {CanBeDeleted}, Value: {Value}")]
internal readonly struct Binding
{
    public Binding(
        JsValue value,
        bool canBeDeleted,
        bool mutable,
        bool strict)
    {
        Value = value;
        CanBeDeleted = canBeDeleted;
        Mutable = mutable;
        Strict = strict;
    }

    public readonly JsValue Value;
    public readonly bool CanBeDeleted;
    public readonly bool Mutable;
    public readonly bool Strict;

    public Binding ChangeValue(JsValue argument)
    {
        return new Binding(argument, CanBeDeleted, Mutable, Strict);
    }

    public bool IsInitialized() => Value is not null;
}
