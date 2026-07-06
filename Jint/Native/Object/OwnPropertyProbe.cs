namespace Jint.Native.Object;

/// <summary>
/// Result of <see cref="ObjectInstance.ProbeOwnProperty"/>: the own property's existence and
/// enumerability without materializing a <see cref="Runtime.Descriptors.PropertyDescriptor"/>.
/// </summary>
internal enum OwnPropertyProbe
{
    Missing,
    NonEnumerable,
    Enumerable,
}
