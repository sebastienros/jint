using System;
using System.Collections.Generic;

namespace Jint.Runtime.Debugger
{
    /// <summary>
    /// BreakLocation is a combination of an Esprima position (line and column) and a source (path or identifier of script).
    /// Like Esprima, first column is 0 and first line is 1.
    /// </summary>
    public sealed record BreakLocation
    {
        public BreakLocation(string source, int line, int column)
        {
            Source = source;
            Line = line;
            Column = column;
        }

        public BreakLocation(int line, int column) : this(null, line, column)
        {

        }

        public BreakLocation(string source, Esprima.Position position) : this(source, position.Line, position.Column)
        {
        }

        public string Source { get; }
        public int Line { get; }
        public int Column { get; }
    }

    /// <summary>
    /// Equality comparer for BreakLocation matching null Source to any other Source.
    /// </summary>
    /// <remarks>
    /// Equals returns true if all properties are equal - or if Source is null on either BreakLocation.
    /// GetHashCode excludes Source.
    /// </remarks>
    public sealed class OptionalSourceBreakLocationEqualityComparer : IEqualityComparer<BreakLocation>
    {
        public bool Equals(BreakLocation x, BreakLocation y)
        {
            if (Object.ReferenceEquals(x, y))
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
                (x.Source == null || y.Source == null || x.Source == y.Source);
        }

        public int GetHashCode(BreakLocation obj)
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
}
