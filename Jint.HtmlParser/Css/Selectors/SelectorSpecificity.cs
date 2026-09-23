namespace Jint.HtmlParser.Css;

internal readonly struct SelectorSpecificity : IEquatable<SelectorSpecificity>, IComparable<SelectorSpecificity>
{
    internal SelectorSpecificity(int idCount, int classCount, int typeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(idCount);
        ArgumentOutOfRangeException.ThrowIfNegative(classCount);
        ArgumentOutOfRangeException.ThrowIfNegative(typeCount);
        IdCount = idCount;
        ClassCount = classCount;
        TypeCount = typeCount;
    }

    internal int IdCount { get; }
    internal int ClassCount { get; }
    internal int TypeCount { get; }

    public int CompareTo(SelectorSpecificity other)
    {
        var value = IdCount.CompareTo(other.IdCount);
        if (value != 0) return value;
        value = ClassCount.CompareTo(other.ClassCount);
        return value != 0 ? value : TypeCount.CompareTo(other.TypeCount);
    }

    public bool Equals(SelectorSpecificity other) =>
        IdCount == other.IdCount && ClassCount == other.ClassCount && TypeCount == other.TypeCount;

    public override bool Equals(object? obj) => obj is SelectorSpecificity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(IdCount, ClassCount, TypeCount);
    public static bool operator ==(SelectorSpecificity left, SelectorSpecificity right) => left.Equals(right);
    public static bool operator !=(SelectorSpecificity left, SelectorSpecificity right) => !left.Equals(right);

    internal static SelectorSpecificity Add(SelectorSpecificity left, SelectorSpecificity right) =>
        new(Saturate(left.IdCount, right.IdCount), Saturate(left.ClassCount, right.ClassCount),
            Saturate(left.TypeCount, right.TypeCount));

    private static int Saturate(int a, int b) => a > int.MaxValue - b ? int.MaxValue : a + b;
}
