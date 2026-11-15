namespace Jint.HtmlParser.Css.Values;

internal sealed class CssValueWork
{
    private readonly CancellationToken _cancellationToken;
    private readonly Action? _checkpoint;
    private readonly CssValueWork _counter;
    private int _sinceCheck;
    internal CancellationToken Token => _cancellationToken;

    internal CssValueWork(CancellationToken cancellationToken, Action? checkpoint = null)
    {
        _cancellationToken = cancellationToken;
        _checkpoint = checkpoint;
        _counter = this;
    }

    // Per-read guards share the invocation's polling remainder instead of restarting its budget.
    private CssValueWork(CssValueWork parent, Action checkpoint)
    {
        _cancellationToken = parent.Token;
        _checkpoint = checkpoint;
        _counter = parent._counter;
    }

    internal static CssValueWork Guard(CssValueWork parent, Action checkpoint) => new(parent, checkpoint);

    internal void CheckCancellation()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _checkpoint?.Invoke();
        _cancellationToken.ThrowIfCancellationRequested();
    }

    internal void Charge(int utf16Units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Units);
        while (utf16Units > 0)
        {
            var available = 4096 - _counter._sinceCheck;
            var charged = System.Math.Min(available, utf16Units);
            _counter._sinceCheck += charged;
            utf16Units -= charged;
            if (_counter._sinceCheck == 4096)
            {
                _counter._sinceCheck = 0;
                CheckCancellation();
            }
        }
    }
}
