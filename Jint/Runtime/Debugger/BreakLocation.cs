namespace Jint.Runtime.Debugger;

/// <summary>
/// BreakLocation is a combination of an Esprima position (line and column) and a source (path or identifier of script).
/// Like Esprima, first column is 0 and first line is 1.
/// </summary>
public sealed record BreakLocation
{
    public BreakLocation(string? source, int line, int column)
    {
        Source = source;
        Line = line;
        Column = column;
    }

    public BreakLocation(int line, int column) : this(null, line, column)
    {

    }

    public BreakLocation(string? source, Position position) : this(source, position.Line, position.Column)
    {
    }

    public string? Source { get; }
    public int Line { get; }
    public int Column { get; }
}
