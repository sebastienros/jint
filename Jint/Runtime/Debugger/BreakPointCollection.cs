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
    private readonly Engine? _engine;
    private bool _active = true;

    public BreakPointCollection()
    {
    }

    internal BreakPointCollection(Engine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Gets or sets whether breakpoints are activated. When false, all breakpoints will fail to match (and be skipped by the debugger).
    /// </summary>
    public bool Active
    {
        get
        {
            if (_engine is null)
            {
                return _active;
            }

            using var ownership = _engine.EnterHostCall();
            return _active;
        }
        set
        {
            if (_engine is null)
            {
                _active = value;
                return;
            }

            using var ownership = _engine.EnterHostCall();
            _active = value;
        }
    }

    public int Count
    {
        get
        {
            if (_engine is null)
            {
                return _breakPoints.Count;
            }

            using var ownership = _engine.EnterHostCall();
            return _breakPoints.Count;
        }
    }

    /// <summary>
    /// Sets a new breakpoint. Note that this will replace any breakpoint at the same location (source/column/line).
    /// </summary>
    public void Set(BreakPoint breakPoint)
    {
        if (_engine is not null)
        {
            using var ownership = _engine.EnterHostCall();
            _breakPoints[breakPoint.Location] = breakPoint;
            return;
        }

        _breakPoints[breakPoint.Location] = breakPoint;
    }

    /// <summary>
    /// Removes breakpoint with the given location (source/column/line).
    /// Note that a null source matches <i>any</i> source.
    /// </summary>
    public bool RemoveAt(BreakLocation location)
    {
        if (_engine is not null)
        {
            using var ownership = _engine.EnterHostCall();
            return _breakPoints.Remove(location);
        }

        return _breakPoints.Remove(location);
    }

    /// <summary>
    /// Checks whether collection contains a breakpoint at the given location (source/column/line).
    /// Note that a null source matches <i>any</i> source.
    /// </summary>
    public bool Contains(BreakLocation location)
    {
        if (_engine is not null)
        {
            using var ownership = _engine.EnterHostCall();
            return _breakPoints.ContainsKey(location);
        }

        return _breakPoints.ContainsKey(location);
    }

    /// <summary>
    /// Removes all breakpoints.
    /// </summary>
    public void Clear()
    {
        if (_engine is not null)
        {
            using var ownership = _engine.EnterHostCall();
            _breakPoints.Clear();
            return;
        }

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
        if (_engine is not null)
        {
            using var ownership = _engine.EnterHostCall();
            return new List<BreakPoint>(_breakPoints.Values).GetEnumerator();
        }

        return _breakPoints.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
