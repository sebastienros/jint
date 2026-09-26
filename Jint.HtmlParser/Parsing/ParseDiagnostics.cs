using System.Collections.ObjectModel;

namespace Jint.HtmlParser;

/// <summary>A parser diagnostic at a zero-based offset in the original UTF-16 input.</summary>
public readonly struct ParseDiagnostic
{
    private readonly string? _code;

    internal ParseDiagnostic(string code, long offset)
    {
        _code = code;
        Offset = offset;
    }

    /// <summary>A stable domain-prefixed diagnostic identifier.</summary>
    public string Code => _code ?? string.Empty;

    /// <summary>The original UTF-16 input offset, or input length for EOF.</summary>
    public long Offset { get; }
}

/// <summary>Collects bounded parser diagnostics in arrival order.</summary>
public sealed class ParseDiagnosticCollector
{
    private readonly List<ParseDiagnostic> _items;
    private readonly ReadOnlyCollection<ParseDiagnostic> _view;

    /// <summary>Creates a collector with a positive record capacity.</summary>
    public ParseDiagnosticCollector(int capacity = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        _items = new List<ParseDiagnostic>();
        _view = _items.AsReadOnly();
    }

    /// <summary>Maximum number of retained diagnostics.</summary>
    public int Capacity { get; }

    /// <summary>A live read-only view of the retained diagnostics.</summary>
    public IReadOnlyList<ParseDiagnostic> Items => _view;

    /// <summary>Whether one or more diagnostics exceeded capacity.</summary>
    public bool IsTruncated { get; private set; }

    /// <summary>Removes all diagnostics and resets truncation.</summary>
    public void Clear()
    {
        _items.Clear();
        IsTruncated = false;
    }

    internal void Add(string code, long offset)
    {
        if (_items.Count == Capacity)
        {
            IsTruncated = true;
            return;
        }

        _items.Add(new ParseDiagnostic(code, offset));
    }
}
