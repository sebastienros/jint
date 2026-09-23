namespace Jint.HtmlParser.Css.Values;

internal sealed class CssValueWork
{
    private readonly CancellationToken _cancellationToken;
    private readonly Action? _checkpoint;
    private int _sinceCheck;

    internal CssValueWork(CancellationToken cancellationToken, Action? checkpoint = null)
    {
        _cancellationToken = cancellationToken;
        _checkpoint = checkpoint;
    }

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
            var available = 4096 - _sinceCheck;
            var charged = System.Math.Min(available, utf16Units);
            _sinceCheck += charged;
            utf16Units -= charged;
            if (_sinceCheck == 4096)
            {
                _sinceCheck = 0;
                CheckCancellation();
            }
        }
    }
}
