using System.Collections;
using System.Threading;

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
    private readonly Lock _lock = new();
    private bool _active = true;

    public BreakPointCollection()
    {
    }

    /// <summary>
    /// Gets or sets whether breakpoints are activated. When false, all breakpoints will fail to match (and be skipped by the debugger).
    /// </summary>
    public bool Active
    {
        get
        {
            lock (_lock)
            {
                return _active;
            }
        }
        set
        {
            lock (_lock)
            {
                _active = value;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _breakPoints.Count;
            }
        }
    }

    /// <summary>
    /// Sets a new breakpoint. Note that this will replace any breakpoint at the same location (source/column/line).
    /// </summary>
    public void Set(BreakPoint breakPoint)
    {
        lock (_lock)
        {
            _breakPoints[breakPoint.Location] = breakPoint;
        }
    }

    /// <summary>
    /// Removes breakpoint with the given location (source/column/line).
    /// Note that a null source matches <i>any</i> source.
    /// </summary>
    public bool RemoveAt(BreakLocation location)
    {
        lock (_lock)
        {
            return _breakPoints.Remove(location);
        }
    }

    /// <summary>
    /// Checks whether collection contains a breakpoint at the given location (source/column/line).
    /// Note that a null source matches <i>any</i> source.
    /// </summary>
    public bool Contains(BreakLocation location)
    {
        lock (_lock)
        {
            return _breakPoints.ContainsKey(location);
        }
    }

    /// <summary>
    /// Removes all breakpoints.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _breakPoints.Clear();
        }
    }

    internal BreakPoint? FindMatch(DebugHandler debugger, BreakLocation location)
    {
        BreakPoint? breakPoint;
        lock (_lock)
        {
            if (!_active || !_breakPoints.TryGetValue(location, out breakPoint))
            {
                return null;
            }
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
        lock (_lock)
        {
            return new List<BreakPoint>(_breakPoints.Values).GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
