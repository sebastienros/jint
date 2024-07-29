using System.Collections;

namespace Jint.Runtime.Debugger;

/// <summary>
/// Collection of breakpoints.
/// </summary>
/// <remarks>
/// Only allows a single breakpoint at the same location (source, column and line).
/// Adding a new breakpoint at the same location <i>replaces</i> the old one - this allows replacing e.g. a
/// conditional breakpoint with a new condition (or remove the condition).
/// </remarks>
public sealed class BreakPointCollection : IEnumerable<BreakPoint>
{
    private readonly Dictionary<BreakLocation, BreakPoint> _breakPoints = new(new OptionalSourceBreakLocationEqualityComparer());

    public BreakPointCollection()
    {
    }

    /// <summary>
    /// Gets or sets whether breakpoints are activated. When false, all breakpoints will fail to match (and be skipped by the debugger).
    /// </summary>
    public bool Active { get; set; } = true;

    public int Count => _breakPoints.Count;

    /// <summary>
    /// Sets a new breakpoint. Note that this will replace any breakpoint at the same location (source/column/line).
    /// </summary>
    public void Set(BreakPoint breakPoint)
    {
        _breakPoints[breakPoint.Location] = breakPoint;
    }

    /// <summary>
    /// Removes breakpoint with the given location (source/column/line).
    /// Note that a null source matches <i>any</i> source.
    /// </summary>
    public bool RemoveAt(BreakLocation location)
    {
        return _breakPoints.Remove(location);
    }

    /// <summary>
    /// Checks whether collection contains a breakpoint at the given location (source/column/line).
    /// Note that a null source matches <i>any</i> source.
    /// </summary>
    public bool Contains(BreakLocation location)
    {
        return _breakPoints.ContainsKey(location);
    }

    /// <summary>
    /// Removes all breakpoints.
    /// </summary>
    public void Clear()
    {
        _breakPoints.Clear();
    }

    internal BreakPoint? FindMatch(DebugHandler debugger, BreakLocation location)
    {
        if (!Active)
        {
            return null;
        }

        if (!_breakPoints.TryGetValue(location, out var breakPoint))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(breakPoint.Condition))
        {
            try
            {
                var completionValue = debugger.Evaluate(breakPoint.Condition!);

                // Truthiness check:
                if (!TypeConverter.ToBoolean(completionValue))
                {
                    return null;
                }
            }
            catch (Exception ex) when (ex is JavaScriptException || ex is DebugEvaluationException)
            {
                // Error in the condition means it doesn't match - shouldn't actually throw.
                return null;
            }
        }

        return breakPoint;
    }

    public IEnumerator<BreakPoint> GetEnumerator()
    {
        return _breakPoints.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _breakPoints.GetEnumerator();
    }
}
