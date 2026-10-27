#nullable enable
using System.Buffers;
using System.Runtime.CompilerServices;
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.IR;

namespace Jint.Runtime.RegExp.GregsEx.Flavors.EcmaScript.Parsing;

// Cold compilation storage. Every input-dependent object/array/string is admitted
// before allocation; old and replacement arrays overlap in the peak accounting.
internal sealed class ParserStorage
{
    internal CompileBudget Budget;
    internal SourceRange Source;
    internal string Feature = "ES-002";
    private long _live;
    internal ParserStorage(CompileBudget budget) { Budget = budget; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Work(long units = 1)
    {
        if (!Budget.TryChargeFast(units))
        {
            WorkSlow(units);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WorkSlow(long units)
    {
        if (!Budget.TryCharge(units, Source, out var diagnostic))
        {
            throw new ParseFailure(diagnostic, Feature);
        }
    }
    internal void Reserve(long bytes)
    {
        if (!Budget.TryReserveBytes(bytes, Source, out var diagnostic))
        {
            throw new ParseFailure(diagnostic, Feature);
        }

        _live += bytes;
    }
    internal void Release(long bytes) { Budget.ReleaseBytes(bytes); _live -= bytes; }
    internal void ReleaseAll()
    {
        Budget.ReleaseBytes(_live);
        _live = 0;
        // Every buffer is scratch: the program copies what it keeps.
        if (_buffers is not null)
        {
            foreach (var buffer in _buffers)
            {
                buffer.ReturnStorage();
            }

            _buffers.Clear();
        }
    }

    // The buffers whose arrays are rented, returned by ReleaseAll.
    private List<IPooledBuffer>? _buffers;
    internal void Track(IPooledBuffer buffer) => (_buffers ??= []).Add(buffer);
    internal void Fail(RegexDiagnosticCode code = RegexDiagnosticCode.InvalidSyntax)
        => throw new ParseFailure(new(code, Source.Offset, Source.Length, Source.Unit), Feature);
    internal Buffer<T> Buffer<T>() { Reserve(64); var buffer = new Buffer<T>(this); Track(buffer); return buffer; }
    internal string Own(ReadOnlySpan<char> text)
    {
        Work(text.Length);
        Reserve(32L + 2L * text.Length);
        return text.ToString();
    }
}

internal sealed class ParseFailure(RegexDiagnostic diagnostic, string feature) : Exception
{
    internal RegexDiagnostic Diagnostic { get; } = diagnostic;
    internal string Feature { get; } = feature;
}

internal interface IPooledBuffer
{
    void ReturnStorage();
}

// Its array is rented: a compile's buffers grow through several sizes, each a garbage array before, and a pattern's
// node buffer alone (about 60 bytes a node) was most of what compiling a short pattern allocated.
internal sealed class Buffer<T>(ParserStorage storage) : IPooledBuffer
{
    private T[] _items = [];
    // The capacity the budget was charged for, which a rented array may exceed.
    private int _capacity;
    internal int Count;
    internal Span<T> Items => _items.AsSpan(0, Count);
    internal ref T this[int index] => ref _items[index];
    internal void Add(T item)
    {
        storage.Work();
        if (Count == _capacity)
        {
            var wanted = Math.Max(4L, 2L * Count);
            if (wanted > int.MaxValue)
            {
                storage.Fail(RegexDiagnosticCode.CompileMemoryLimitExceeded);
            }

            var capacity = (int)wanted;
            var bytes = 32L + (long)Unsafe.SizeOf<T>() * capacity;
            storage.Reserve(bytes);
            storage.Work(Count);
            if (capacity > _items.Length)
            {
                var replacement = ArrayPool<T>.Shared.Rent(capacity);
                Items.CopyTo(replacement);
                Return(_items);
                _items = replacement;
            }

            if (_capacity != 0)
            {
                storage.Release(32L + (long)Unsafe.SizeOf<T>() * _capacity);
            }

            _capacity = capacity;
        }
        _items[Count++] = item;
    }
    internal T Pop() { storage.Work(); var value = _items[--Count]; _items[Count] = default!; return value; }
    internal void Remove(int index, int length = 1)
    {
        storage.Work(Count - index);
        _items.AsSpan(index + length, Count - index - length).CopyTo(_items.AsSpan(index));
        _items.AsSpan(Count - length, length).Clear();
        Count -= length;
    }
    internal void Dispose()
    {
        if (_capacity != 0)
        {
            storage.Release(32L + (long)Unsafe.SizeOf<T>() * _capacity);
        }

        storage.Release(64);
        ReturnStorage();
    }

    public void ReturnStorage()
    {
        Return(_items);
        _items = [];
        _capacity = 0;
        Count = 0;
    }

#if NETCOREAPP
    private static readonly bool ClearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
    private static readonly bool ClearOnReturn = true;
#endif

    private static void Return(T[] items)
    {
        if (items.Length != 0)
        {
            // Cleared only when the element type holds strings or arrays; the node, interval and index buffers hold
            // neither, and every compile cleared them for nothing.
            ArrayPool<T>.Shared.Return(items, clearArray: ClearOnReturn);
        }
    }
}
