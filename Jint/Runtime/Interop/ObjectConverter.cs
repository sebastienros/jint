using System.Diagnostics.CodeAnalysis;
using Jint.Native;

namespace Jint.Runtime.Interop;

/// <summary>
/// Converts a CLR value to a <see cref="JsValue"/> on its way into script.
/// </summary>
/// <remarks>
/// Register one with <see cref="OptionsExtensions.AddObjectConverter(Options, ObjectConverter)"/>. Every CLR
/// value crossing into script is offered to each registered converter before the engine's own conversion runs,
/// so a converter that claims every value takes the compiled interop member-read and method-invoker lanes off
/// every wrapped member. Declaring <see cref="HandledTypes"/> — here or at the registration — is what buys
/// them back for the members no declared type can reach.
/// </remarks>
public abstract class ObjectConverter
{
    /// <summary>
    /// Converts <paramref name="value"/>, returning <see langword="false"/> to leave it to the next converter
    /// and finally to the engine.
    /// </summary>
    public abstract bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result);

    /// <summary>
    /// The CLR types of the values this converter converts, or <see langword="null"/> — the default — to be
    /// offered every value.
    /// </summary>
    /// <remarks>
    /// A declaration is a promise the engine takes at face value: it keeps its compiled member-read and
    /// method-invoker lanes for every member and method whose <em>declared</em> type cannot produce a value of
    /// any declared type, and this converter is never consulted for those. Assignability is the rule, in both
    /// directions — a member typed <see cref="object"/> can hold anything, so no declaration excludes it.
    /// <para>
    /// Override this or use
    /// <see cref="OptionsExtensions.AddObjectConverter(Options, ObjectConverter, Type[])"/>; declaring in both
    /// places claims the union, so a registration can widen a converter's own declaration but never narrow it.
    /// Add a case to <see cref="TryConvert"/> without adding its type here and the case is silently skipped on
    /// exactly the members the declaration excluded — run with host-contract verification on, which reports it.
    /// </para>
    /// </remarks>
    protected internal virtual Type[]? HandledTypes => null;
}
