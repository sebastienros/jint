namespace Jint.HtmlParser;

// Stored offsets are unsigned values, including values beyond the container's current length.
internal readonly record struct BoundaryPoint(DomNodeIdentity Container, uint Offset);
