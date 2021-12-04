using System;
using System.Collections;
using System.Collections.Generic;

namespace Jint.Runtime.Debugger
{
    public sealed class BreakLocation : IEquatable<BreakLocation>
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

        public bool Equals(BreakLocation other)
        {
            if (other is null)
            {
                return false;
            }

            return other.Line == Line &&
                other.Column == Column &&
                other.Source == Source;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BreakLocation);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 33 + Line.GetHashCode();
                hash = hash * 33 + Column.GetHashCode();
                hash = hash * 33 + Source.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(BreakLocation a, BreakLocation b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(BreakLocation a, BreakLocation b)
        {
            return !Equals(a, b);
        }
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
