#if NET8_0_OR_GREATER
using System.Linq;
using System.Globalization;
using System.Text;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.IndexedDb;

/// <summary>Key paths over serialized values: https://w3c.github.io/IndexedDB/#key-path-construct.</summary>
internal sealed class IndexedDbKeyPath(string[] paths, bool sequence)
{
    internal string[] Paths { get; } = paths;
    internal bool IsSequence { get; } = sequence;

    internal static IndexedDbKeyPath Read(Engine engine, Realm realm, JsValue value)
    {
        var sequence = value is ObjectInstance obj && obj.GetMethod(GlobalSymbolRegistry.Iterator) is not null;
        var paths = new List<string>();
        if (sequence)
        {
            var iterator = value.GetIterator(realm);
            try
            {
                while (iterator.TryIteratorStepValue(out var item))
                {
                    engine.Constraints.Check();
                    paths.Add(TypeConverter.ToString(item));
                }
            }
            catch
            {
                iterator.Close(CompletionType.Throw);
                throw;
            }
        }
        else
        {
            paths.Add(TypeConverter.ToString(value));
        }
        if (paths.Count == 0 || paths.Exists(static path => !Valid(path)))
        {
            IndexedDbErrors.Throw(realm, "SyntaxError", "The key path is not a valid identifier path.");
        }
        return new IndexedDbKeyPath(paths.ToArray(), sequence);
    }

    private static bool Valid(string path)
    {
        if (path.Length == 0) return true;
        foreach (var part in path.Split('.'))
        {
            if (part.Length == 0) return false;
            var first = true;
            foreach (var rune in part.EnumerateRunes())
            {
                var category = Rune.GetUnicodeCategory(rune);
                var start = rune.Value is '$' or '_' or 0x1885 or 0x1886 or 0x2118 or 0x212E or 0x309B or 0x309C
                    || category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                        or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                        or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
                var continuation = start || rune.Value is 0x200C or 0x200D or 0x00B7 or 0x0387 or 0x19DA
                    || rune.Value is >= 0x1369 and <= 0x1371
                    || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                        or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
                if (first ? !start : !continuation) return false;
                first = false;
            }
        }
        return true;
    }

    internal JsValue ToValue(Engine engine)
        => IsSequence ? new JsArray(engine, Paths.Select(static p => (JsValue) JsString.Create(p)).ToArray()) : JsString.Create(Paths[0]);

    /// <summary>https://w3c.github.io/IndexedDB/#evaluate-a-key-path-on-a-value.</summary>
    internal bool TryEvaluate(SerializedValue root, out SerializedValue value)
    {
        if (!IsSequence) return TryEvaluate(root, Paths[0], out value);
        var array = new SerializedArray((uint) Paths.Length);
        for (var i = 0; i < Paths.Length; i++)
        {
            if (!TryEvaluate(root, Paths[i], out var item))
            {
                value = default;
                return false;
            }
            array.Properties.Add(new SerializedProperty(i.ToString(CultureInfo.InvariantCulture), item));
        }
        value = SerializedValue.FromObject(array);
        return true;
    }

    private static bool TryEvaluate(SerializedValue root, string path, out SerializedValue value)
    {
        value = root;
        if (path.Length == 0) return true;
        foreach (var part in path.Split('.'))
        {
            if (!TryProperty(value, part, out value) || value.Kind == SerializedValueKind.Undefined) return false;
        }
        return true;
    }

    private static bool TryProperty(SerializedValue value, string name, out SerializedValue result)
    {
        result = default;
        if (value.Kind == SerializedValueKind.String && name is "length")
        {
            result = SerializedValue.FromNumber(value.AsString().Length);
            return true;
        }
        if (value.Kind != SerializedValueKind.Object) return false;
        var obj = value.AsObject();
        result = obj switch
        {
            SerializedArray a when name is "length" => SerializedValue.FromNumber(a.Length),
            SerializedBlob blob when name is "size" => SerializedValue.FromNumber(blob.Bytes.Length),
            SerializedBlob blob when name is "type" => SerializedValue.FromString(blob.MediaType),
            SerializedFile file when name is "name" => SerializedValue.FromString(file.Name),
            SerializedFile file when name is "lastModified" => SerializedValue.FromNumber(file.LastModified),
            _ => default,
        };
        if (result.Kind != SerializedValueKind.Undefined) return true;
        if (Properties(obj) is not { } properties) return false;
        foreach (var property in properties)
        {
            if (!string.Equals(property.Key, name, StringComparison.Ordinal)) continue;
            result = property.Value;
            return true;
        }
        return false;
    }

    /// <summary>https://w3c.github.io/IndexedDB/#inject-key-into-value.</summary>
    internal bool Inject(SerializedValue root, double number, bool checkOnly)
    {
        var parts = Paths[0].Split('.');
        var current = root;
        for (var i = 0; i < parts.Length; i++)
        {
            if (current.Kind != SerializedValueKind.Object || Properties(current.AsObject()) is not { } properties) return false;
            var last = i == parts.Length - 1;
            if (TryProperty(current, parts[i], out var existing) && !last)
            {
                current = existing;
                continue;
            }
            if (checkOnly) return true;
            var next = last ? SerializedValue.FromNumber(number) : SerializedValue.FromObject(new SerializedPlainObject());
            var index = properties.FindIndex(p => string.Equals(p.Key, parts[i], StringComparison.Ordinal));
            var property = new SerializedProperty(parts[i], next);
            if (index < 0) properties.Add(property);
            else properties[index] = property;
            current = next;
        }
        return true;
    }

    private static List<SerializedProperty>? Properties(SerializedObject value) => value switch
    {
        SerializedPlainObject plain => plain.Properties,
        SerializedArray array => array.Properties,
        _ => null,
    };

    internal static IndexedDbKey? ToKey(SerializedValue value, Action check, HashSet<SerializedArray>? seen = null)
    {
        var pending = new Stack<KeyFrame>();
        seen ??= new HashSet<SerializedArray>(ReferenceEqualityComparer.Instance);
        var visited = 0;
        while (true)
        {
            if ((++visited & 255) == 0) check();
            IndexedDbKey? key;
            if (value.Kind == SerializedValueKind.Object && value.AsObject() is SerializedArray array)
            {
                if (!seen.Add(array)) return null;
                var keys = new List<IndexedDbKey>();
                if (array.Length != 0)
                {
                    if (!TryProperty(value, "0", out var first)) return null;
                    pending.Push(new KeyFrame(value, keys, array.Length));
                    value = first;
                    continue;
                }
                key = IndexedDbKey.Array([]);
            }
            else
            {
                key = value.Kind switch
                {
                    SerializedValueKind.Number when !double.IsNaN(value.AsNumber()) => IndexedDbKey.Numeric(value.AsNumber()),
                    SerializedValueKind.String => IndexedDbKey.String(value.AsString()),
                    SerializedValueKind.Object => ObjectKey(value.AsObject()),
                    _ => null,
                };
                if (key is null) return null;
            }
            while (true)
            {
                if (pending.Count == 0) return key;
                var frame = pending.Pop();
                frame.Keys.Add(key);
                var next = frame.Keys.Count;
                if ((uint) next < frame.Length)
                {
                    if (!TryProperty(frame.Value, next.ToString(CultureInfo.InvariantCulture), out value)) return null;
                    pending.Push(frame);
                    break;
                }
                key = IndexedDbKey.Array(frame.Keys.ToArray());
            }
        }
    }

    private static IndexedDbKey? ObjectKey(SerializedObject value) => value switch
    {
        SerializedDate date when date.DateValue.IsFinite => IndexedDbKey.Numeric(date.DateValue.Value, date: true),
        SerializedArrayBuffer buffer => IndexedDbKey.Binary(buffer.Bytes.ToArray()),
        SerializedArrayBufferView view => IndexedDbKey.Binary(view.Buffer.Bytes.AsSpan((int) view.ByteOffset,
            view.Length is { } length ? checked((int) length * (view.ElementType?.GetElementSize() ?? 1))
                : view.Buffer.Bytes.Length - (int) view.ByteOffset).ToArray()),
        _ => null,
    };

    internal SortedSet<IndexedDbKey> IndexKeys(SerializedValue root, bool multiEntry, Action check)
    {
        var keys = new SortedSet<IndexedDbKey>();
        if (!TryEvaluate(root, out var value)) return keys;
        if (multiEntry && value.Kind == SerializedValueKind.Object && value.AsObject() is SerializedArray array)
        {
            var seen = new HashSet<SerializedArray>(ReferenceEqualityComparer.Instance) { array };
            foreach (var property in array.Properties)
            {
                check();
                if (uint.TryParse(property.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    && index < array.Length && ToKey(property.Value, check, seen) is { } key)
                {
                    keys.Add(key);
                }
            }
        }
        else if (ToKey(value, check) is { } key)
        {
            keys.Add(key);
        }
        return keys;
    }

    private readonly record struct KeyFrame(SerializedValue Value, List<IndexedDbKey> Keys, uint Length);
}
#endif
