namespace Jint.Runtime.Debugger;

// BreakPoint is not sealed. It's useful to be able to add additional properties on a derived BreakPoint class (e.g. a breakpoint ID
// or breakpoint type) but still let it be managed by Jint's breakpoint collection.
public class BreakPoint
{
    public BreakPoint(string? source, int line, int column, string? condition = null)
    {
        Location = new BreakLocation(source, line, column);
        Condition = condition;
    }

    public BreakPoint(int line, int column, string? condition = null) : this(null, line, column, condition)
    {
    }

    public BreakLocation Location { get; }
    public string? Condition { get; }
}
