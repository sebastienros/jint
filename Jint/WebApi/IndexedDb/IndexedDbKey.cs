#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.Encoding;

namespace Jint.WebApi.IndexedDb;

/// <summary>An engine-free key ordered by https://w3c.github.io/IndexedDB/#compare-two-keys.</summary>
internal sealed class IndexedDbKey : IComparable<IndexedDbKey>, IEquatable<IndexedDbKey>
{
    internal enum KeyKind : byte { Number, Date, String, Binary, Array }

    private IndexedDbKey(KeyKind kind) => Kind = kind;

    internal KeyKind Kind { get; }
    internal double Number { get; private init; }
    internal string Text { get; private init; } = "";
    internal byte[] Bytes { get; private init; } = [];
    internal IndexedDbKey[] Items { get; private init; } = [];

    internal static IndexedDbKey Numeric(double value, bool date = false)
        => new(date ? KeyKind.Date : KeyKind.Number) { Number = value == 0 ? 0 : value };
    internal static IndexedDbKey String(string value) => new(KeyKind.String) { Text = value };
    internal static IndexedDbKey Binary(byte[] value) => new(KeyKind.Binary) { Bytes = value };
    internal static IndexedDbKey Array(IndexedDbKey[] value) => new(KeyKind.Array) { Items = value };

    public bool Equals(IndexedDbKey? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is IndexedDbKey other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        var pending = new Stack<IndexedDbKey>();
        pending.Push(this);
        while (pending.TryPop(out var key))
        {
            hash.Add(key.Kind);
            hash.Add(key.Number);
            hash.Add(key.Text, StringComparer.Ordinal);
            hash.Add(key.Bytes.Length);
            foreach (var item in key.Bytes) hash.Add(item);
            hash.Add(key.Items.Length);
            foreach (var item in key.Items) pending.Push(item);
        }
        return hash.ToHashCode();
    }

    public int CompareTo(IndexedDbKey? other)
    {
        if (other is null) return 1;
        var left = this;
        var right = other;
        Stack<ArrayComparison>? pending = null;
        while (true)
        {
            var comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0) return comparison;
            switch (left.Kind)
            {
                case KeyKind.Number:
                case KeyKind.Date:
                    comparison = left.Number.CompareTo(right.Number);
                    break;
                case KeyKind.String:
                    comparison = string.CompareOrdinal(left.Text, right.Text);
                    break;
                case KeyKind.Binary:
                    comparison = left.Bytes.AsSpan().SequenceCompareTo(right.Bytes);
                    break;
                case KeyKind.Array:
                    if (left.Items.Length != 0 && right.Items.Length != 0)
                    {
                        (pending ??= new()).Push(new ArrayComparison(left.Items, right.Items, 1));
                        left = left.Items[0];
                        right = right.Items[0];
                        continue;
                    }
                    comparison = left.Items.Length.CompareTo(right.Items.Length);
                    break;
            }
            if (comparison != 0) return System.Math.Sign(comparison);
            while (true)
            {
                if (pending is not { Count: > 0 }) return 0;
                var frame = pending.Pop();
                if (frame.Index < System.Math.Min(frame.Left.Length, frame.Right.Length))
                {
                    pending.Push(frame with { Index = frame.Index + 1 });
                    left = frame.Left[frame.Index];
                    right = frame.Right[frame.Index];
                    break;
                }
                comparison = frame.Left.Length.CompareTo(frame.Right.Length);
                if (comparison != 0) return comparison;
            }
        }
    }

    private readonly record struct ArrayComparison(IndexedDbKey[] Left, IndexedDbKey[] Right, int Index);
}

/// <summary>Converts keys at the engine boundary, per https://w3c.github.io/IndexedDB/#convert-value-to-key.</summary>
internal static class IndexedDbKeyConverter
{
    internal static IndexedDbKey Require(Engine engine, Realm realm, JsValue value)
        => TryConvert(engine, value) ?? IndexedDbErrors.Throw<IndexedDbKey>(realm, "DataError", "The value is not a valid IndexedDB key.");

    internal static IndexedDbKey? TryConvert(Engine engine, JsValue value)
    {
        var seen = new HashSet<JsArray>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<KeyFrame>();
        var current = value;
        var visited = 0;
        while (true)
        {
            if ((++visited & 255) == 0) engine.Constraints.Check();
            IndexedDbKey? key;
            if (current is JsArray array)
            {
                if (!seen.Add(array)) return null;
                var length = array.GetLength();
                var items = new List<IndexedDbKey>();
                if (length != 0)
                {
                    if (!array.HasOwnProperty(JsNumber.PositiveZero)) return null;
                    pending.Push(new KeyFrame(array, items, length));
                    current = array.Get(JsNumber.PositiveZero);
                    continue;
                }
                key = IndexedDbKey.Array([]);
            }
            else
            {
                key = Scalar(current);
                if (key is null) return null;
            }

            while (true)
            {
                if (pending.Count == 0) return key;
                var frame = pending.Pop();
                frame.Items.Add(key);
                var next = frame.Items.Count;
                if ((uint) next < frame.Length)
                {
                    var property = JsNumber.Create(next);
                    if (!frame.Source.HasOwnProperty(property)) return null;
                    current = frame.Source.Get(property);
                    pending.Push(frame);
                    break;
                }
                key = IndexedDbKey.Array(frame.Items.ToArray());
            }
        }
    }

    private static IndexedDbKey? Scalar(JsValue value)
    {
        if (value.IsNumber())
        {
            var number = value.AsNumber();
            return double.IsNaN(number) ? null : IndexedDbKey.Numeric(number);
        }
        if (value.IsString()) return IndexedDbKey.String(value.AsString());
        if (value is JsDate date)
        {
            return double.IsNaN(date.DateValue) ? null : IndexedDbKey.Numeric(date.DateValue, date: true);
        }
        var buffer = value switch
        {
            JsArrayBuffer direct => direct,
            JsTypedArray typed => typed._viewedArrayBuffer,
            JsDataView view => view._viewedArrayBuffer,
            _ => null,
        };
        if (buffer is null || buffer.IsDetachedBuffer || buffer.IsSharedArrayBuffer) return null;
        return BufferSource.TryGetBytes(value, out var bytes) ? IndexedDbKey.Binary(bytes.ToArray()) : null;
    }

    internal static JsValue ToValue(Engine engine, Realm realm, IndexedDbKey key)
    {
        using var scope = new RealmScope(engine, realm);
        var pending = new Stack<ValueFrame>();
        var current = key;
        var visited = 0;
        while (true)
        {
            if ((++visited & 255) == 0) engine.Constraints.Check();
            JsValue result;
            switch (current.Kind)
            {
                case IndexedDbKey.KeyKind.Number:
                    result = JsNumber.Create(current.Number);
                    break;
                case IndexedDbKey.KeyKind.Date:
                    result = new JsDate(engine, (long) current.Number);
                    break;
                case IndexedDbKey.KeyKind.String:
                    result = JsString.Create(current.Text);
                    break;
                case IndexedDbKey.KeyKind.Binary:
                    var buffer = realm.Intrinsics.ArrayBuffer.Construct((uint) current.Bytes.Length);
                    current.Bytes.CopyTo(buffer.ArrayBufferData!, 0);
                    result = buffer;
                    break;
                default:
                    var values = new JsValue[current.Items.Length];
                    if (values.Length != 0)
                    {
                        pending.Push(new ValueFrame(current.Items, values, 0));
                        current = current.Items[0];
                        continue;
                    }
                    result = new JsArray(engine, values);
                    break;
            }
            while (true)
            {
                if (pending.Count == 0) return result;
                var frame = pending.Pop();
                frame.Values[frame.Index] = result;
                var next = frame.Index + 1;
                if (next < frame.Values.Length)
                {
                    pending.Push(frame with { Index = next });
                    current = frame.Keys[next];
                    break;
                }
                result = new JsArray(engine, frame.Values);
            }
        }
    }

    private readonly record struct KeyFrame(JsArray Source, List<IndexedDbKey> Items, uint Length);
    private readonly record struct ValueFrame(IndexedDbKey[] Keys, JsValue[] Values, int Index);
}
#endif
