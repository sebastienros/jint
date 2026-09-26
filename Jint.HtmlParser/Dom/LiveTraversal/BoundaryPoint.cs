namespace Jint.HtmlParser;

// Stored offsets are unsigned values, including values beyond the container's current length.
/// <summary>An immutable native boundary point with an unsigned UTF-16 or child offset; construction does not validate it.</summary>
/// <param name="Container">The exact native container identity.</param>
/// <param name="Offset">An unvalidated unsigned offset.</param>
public readonly record struct BoundaryPoint(DomNodeIdentity Container, uint Offset);
