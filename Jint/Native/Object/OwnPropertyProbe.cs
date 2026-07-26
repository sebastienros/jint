namespace Jint.Native.Object;

/// <summary>
/// Result of <see cref="ObjectInstance.ProbeOwnProperty"/>: the own property's existence and
/// enumerability without materializing a <see cref="Runtime.Descriptors.PropertyDescriptor"/>.
/// </summary>
public enum OwnPropertyProbe
{
    /// <summary>
    /// The object has no own property with this key. Callers treat this exactly as a
    /// <see cref="Runtime.Descriptors.PropertyDescriptor.Undefined"/> result from
    /// <see cref="ObjectInstance.GetOwnProperty"/>.
    /// </summary>
    Missing,

    /// <summary>
    /// The own property exists but is not enumerable, so key enumeration and copying operations
    /// skip it while existence checks (<c>in</c>, <c>hasOwnProperty</c>) still see it.
    /// </summary>
    NonEnumerable,

    /// <summary>
    /// The own property exists and is enumerable.
    /// </summary>
    Enumerable,
}
