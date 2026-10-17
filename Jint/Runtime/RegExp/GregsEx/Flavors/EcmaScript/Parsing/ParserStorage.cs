#nullable enable
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
    internal void Work(long units = 1)
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
    internal void ReleaseAll() { Budget.ReleaseBytes(_live); _live = 0; }
    internal void Fail(RegexDiagnosticCode code = RegexDiagnosticCode.InvalidSyntax)
        => throw new ParseFailure(new(code, Source.Offset, Source.Length, Source.Unit), Feature);
    internal Buffer<T> Buffer<T>() { Reserve(64); return new(this); }
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

internal sealed class Buffer<T>(ParserStorage storage)
{
    private T[] _items = [];
    internal int Count;
    internal Span<T> Items => _items.AsSpan(0, Count);
    internal ref T this[int index] => ref _items[index];
    internal void Add(T item)
    {
        storage.Work();
        if (Count == _items.Length)
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
            var replacement = new T[capacity];
            Items.CopyTo(replacement);
            if (_items.Length != 0)
            {
                storage.Release(32L + (long)Unsafe.SizeOf<T>() * _items.Length);
            }

            _items = replacement;
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
        if (_items.Length != 0)
        {
            storage.Release(32L + (long)Unsafe.SizeOf<T>() * _items.Length);
        }

        storage.Release(64);
        _items = [];
        Count = 0;
    }
}
