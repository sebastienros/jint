using System.Buffers;
using System.Runtime.CompilerServices;

namespace Jint.HtmlParser.Serialization;

// Bounded UTF-16 output builder. The buffer is private to one serialization call.
internal sealed class SerializationWriter
{
    private const int CopySlice = 256;
    private readonly SerializationWork _work;
    private readonly long _limit;
    private char[] _buffer = Array.Empty<char>();
    private int _length;

    internal SerializationWriter(SerializationWork work, SerializationLimits? limits = null)
    {
        _work = work ?? throw new ArgumentNullException(nameof(work));
        _limit = (limits ?? SerializationLimits.Unbounded).MaxOutputCharacters;
    }

    internal SerializationWork Work => _work;
    internal int Length => _length;

    internal void Append(char value)
    {
        Reserve(1);
        _buffer[_length++] = value;
        _work.Charge(1, SerializationStage.Append);
    }

    internal void Append(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Append(value.AsSpan());
    }

    internal void Append(ReadOnlySpan<char> value)
    {
        // The caller may pass a span over this writer's own buffer only if no growth occurs.
        // Serializer callers pass native strings or static literals, never that buffer.
        Reserve(value.Length);
        if (value.Length <= CopySlice)
        {
            value.CopyTo(_buffer.AsSpan(_length));
            _length += value.Length;
            _work.Charge(value.Length, SerializationStage.Append);
            return;
        }
        for (var offset = 0; offset < value.Length;)
        {
            var count = Math.Min(CopySlice, value.Length - offset);
            value.Slice(offset, count).CopyTo(_buffer.AsSpan(_length, count));
            _length += count;
            _work.Charge(count, SerializationStage.Append);
            offset += count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reserve(int count)
    {
        if (_limit == 0 && (uint) count <= (uint) (_buffer.Length - _length)) return;
        ReserveSlow(count);
    }

    private void ReserveSlow(int count)
    {
        var observed = (long) _length + count;
        if (_limit != 0 && observed > _limit)
        {
            throw new SerializationLimitException(_limit, observed);
        }

        if (observed > Array.MaxLength)
        {
            throw new OverflowException("Serialization output exceeds the CLR array limit.");
        }

        if (observed <= _buffer.Length)
        {
            return;
        }

        _work.Poll(SerializationStage.Append);
        var doubled = _buffer.Length > Array.MaxLength / 2 ? Array.MaxLength : _buffer.Length * 2;
        var expanded = ArrayPool<char>.Shared.Rent(Math.Max((int) observed, Math.Max(256, doubled)));
        _work.Poll(SerializationStage.Append);
        for (var offset = 0; offset < _length;)
        {
            var copied = Math.Min(CopySlice, _length - offset);
            _buffer.AsSpan(offset, copied).CopyTo(expanded.AsSpan(offset, copied));
            _work.Charge(copied, SerializationStage.Append);
            offset += copied;
        }

        ReturnBuffer();
        _buffer = expanded;
        _work.Poll(SerializationStage.Append);
    }

    internal string Materialize()
    {
        _work.Poll(SerializationStage.Materialize);
        var result = string.Create(_length, this, static (destination, writer) =>
        {
            for (var offset = 0; offset < destination.Length;)
            {
                var count = Math.Min(CopySlice, destination.Length - offset);
                writer._buffer.AsSpan(offset, count).CopyTo(destination.Slice(offset, count));
                writer._work.Charge(count, SerializationStage.Materialize);
                offset += count;
            }
        });
        ReturnBuffer();
        _length = 0;
        _work.Poll(SerializationStage.Materialize);
        _work.Complete();
        return result;
    }

    // The buffer is pooled; an operation that throws simply leaves its buffer to the collector.
    private void ReturnBuffer()
    {
        if (_buffer.Length != 0) ArrayPool<char>.Shared.Return(_buffer);
        _buffer = [];
    }
}
