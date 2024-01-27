using System.Diagnostics.CodeAnalysis;

namespace Jint.Runtime.Interop;

/// <summary>
/// Handles conversions between CLR types.
/// </summary>
public interface ITypeConverter
{
    /// <summary>
    /// Converts value to to type. Throws exception if cannot be done.
    /// </summary>
    object? Convert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider);

    /// <summary>
    /// Converts value to to type. Returns false if cannot be done.
    /// </summary>
    bool TryConvert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider,
        [NotNullWhen(true)] out object? converted);
}
