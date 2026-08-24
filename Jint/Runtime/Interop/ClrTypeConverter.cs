using System.Diagnostics.CodeAnalysis;

namespace Jint.Runtime.Interop;

/// <summary>
/// Converts a value to a requested CLR type on its way out of script — an argument, a member write, an
/// indexer key.
/// </summary>
/// <remarks>
/// Register one with <see cref="OptionsExtensions.SetTypeConverter(Options, Func{Engine, ClrTypeConverter})"/>.
/// Both entry points are driven by the <em>target</em> <see cref="Type"/>, which is what
/// <see cref="HandledTargetTypes"/> declares: an engine whose converter declares nothing has to assume every
/// conversion may differ from the stock one, and takes the compiled member-write and method-invoker lanes off
/// every member for it.
/// <para>
/// Derive from <see cref="DefaultTypeConverter"/> to adjust individual conversions and inherit the rest.
/// </para>
/// </remarks>
public abstract class ClrTypeConverter
{
    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="type"/>. Throws if it cannot be done.
    /// </summary>
    public abstract object? Convert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider);

    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="type"/>. Returns <see langword="false"/> if it
    /// cannot be done.
    /// </summary>
    public abstract bool TryConvert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider,
        [NotNullWhen(true)] out object? converted);

    /// <summary>
    /// The CLR <em>target</em> types this converter answers differently from <see cref="DefaultTypeConverter"/>,
    /// or <see langword="null"/> — the default — to be consulted for every target type.
    /// </summary>
    /// <remarks>
    /// A declaration is a promise that for every <em>other</em> target type this converter produces exactly what
    /// <see cref="DefaultTypeConverter"/> would, and in exchange the engine keeps its compiled member-write and
    /// method-invoker lanes for members and parameters typed that way. Unlike
    /// <see cref="ObjectConverter.HandledTypes"/> the question here is exact rather than a guess about a runtime
    /// value: the engine knows the very <see cref="Type"/> this converter would be handed, so a declared type
    /// claims itself and its subtypes and nothing above them.
    /// <para>
    /// Override this or use
    /// <see cref="OptionsExtensions.SetTypeConverter(Options, Func{Engine, ClrTypeConverter}, Type[])"/>;
    /// declaring in both places claims the union. Run with host-contract verification on to have the promise
    /// checked against what the converter actually produces.
    /// </para>
    /// </remarks>
    protected internal virtual Type[]? HandledTargetTypes => null;
}
