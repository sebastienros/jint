namespace Jint.HtmlParser.Serialization;

internal enum SerializationStage
{
    Entry,
    Scan,
    Append,
    Materialize,
    Final
}

// One cadence is shared by the writer and every scalar/structure scan in a call.
internal sealed class SerializationWork
{
    private const int Cadence = 256;
    private readonly CancellationToken _cancellationToken;
    private readonly Action<SerializationStage>? _checkpoint;
    private int _sincePoll;

    internal SerializationWork(CancellationToken cancellationToken, Action<SerializationStage>? checkpoint = null)
    {
        _cancellationToken = cancellationToken;
        _checkpoint = checkpoint;
        Poll(SerializationStage.Entry);
    }

    internal void Poll(SerializationStage stage)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _checkpoint?.Invoke(stage);
        _cancellationToken.ThrowIfCancellationRequested();
    }

    internal void Charge(int units, SerializationStage stage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        while (units != 0)
        {
            var amount = Math.Min(units, Cadence - _sincePoll);
            _sincePoll += amount;
            units -= amount;
            if (_sincePoll == Cadence)
            {
                _sincePoll = 0;
                Poll(stage);
            }
        }
    }

    internal void Complete() => Poll(SerializationStage.Final);
}
