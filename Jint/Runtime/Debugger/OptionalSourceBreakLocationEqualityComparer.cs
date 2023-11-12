namespace Jint.Runtime.Debugger;

/// <summary>
/// Equality comparer for BreakLocation matching null Source to any other Source.
/// </summary>
/// <remarks>
/// Equals returns true if all properties are equal - or if Source is null on either BreakLocation.
/// GetHashCode excludes Source.
/// </remarks>
internal sealed class OptionalSourceBreakLocationEqualityComparer : IEqualityComparer<BreakLocation>
{
    public bool Equals(BreakLocation? x, BreakLocation? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return
            x.Line == y.Line &&
            x.Column == y.Column &&
            (x.Source == null || y.Source == null || string.Equals(x.Source, y.Source, StringComparison.Ordinal));
    }

    public int GetHashCode(BreakLocation? obj)
    {
        if (obj == null)
        {
            return 0;
        }
        // Keeping this rather than HashCode.Combine, which isn't in net461 or netstandard2.0
        unchecked
        {
            int hash = 17;
            hash = hash * 33 + obj.Line.GetHashCode();
            hash = hash * 33 + obj.Column.GetHashCode();
            // Don't include Source
            return hash;
        }
    }
}
