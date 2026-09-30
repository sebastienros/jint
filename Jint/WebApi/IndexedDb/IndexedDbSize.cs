#if NET8_0_OR_GREATER
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.IndexedDb;

/// <summary>Accounts retained serialized payloads without recursion or engine references.</summary>
internal static class IndexedDbSize
{
    internal static long Key(IndexedDbKey key)
    {
        var pending = new Stack<IndexedDbKey>();
        pending.Push(key);
        long size = 0;
        while (pending.TryPop(out var current))
        {
            size = checked(size + 32 + 2L * current.Text.Length + current.Bytes.Length + 8L * current.Items.Length);
            foreach (var item in current.Items) pending.Push(item);
        }
        return size;
    }

    internal static long Record(in SerializationRecord record, Action check)
    {
        var pending = new Stack<SerializedValue>();
        var seen = new HashSet<SerializedObject>(ReferenceEqualityComparer.Instance);
        pending.Push(record.Root);
        long bytes = 0;
        var visited = 0;
        while (pending.TryPop(out var value))
        {
            if ((++visited & 255) == 0) check();
            bytes = checked(bytes + 24);
            if (value.Kind == SerializedValueKind.String) bytes = checked(bytes + 2L * value.AsString().Length);
            if (value.Kind == SerializedValueKind.BigInt) bytes = checked(bytes + value.AsBigInt().GetByteCount());
            if (value.Kind != SerializedValueKind.Object || !seen.Add(value.AsObject())) continue;
            bytes = checked(bytes + 64);
            switch (value.AsObject())
            {
                case SerializedPlainObject plain:
                    Properties(plain.Properties);
                    break;
                case SerializedArray array:
                    Properties(array.Properties);
                    break;
                case SerializedMap map:
                    foreach (var item in map.Entries) { pending.Push(item.Key); pending.Push(item.Value); }
                    break;
                case SerializedSet set:
                    foreach (var item in set.Entries) pending.Push(item);
                    break;
                case SerializedArrayBuffer buffer:
                    bytes = checked(bytes + buffer.Bytes.Length);
                    break;
                case SerializedArrayBufferView view:
                    pending.Push(SerializedValue.FromObject(view.Buffer));
                    break;
                case SerializedBlob blob:
                    bytes = checked(bytes + blob.Bytes.Length + 2L * blob.MediaType.Length);
                    if (blob is SerializedFile file) bytes = checked(bytes + 2L * file.Name.Length);
                    break;
                case SerializedBoxedPrimitive boxed:
                    pending.Push(boxed.Value);
                    break;
                case SerializedError error:
                    bytes = checked(bytes + 2L * ((error.Message?.Length ?? 0) + (error.Stack?.Length ?? 0)));
                    if (error.HasCause) pending.Push(error.Cause);
                    break;
                case SerializedDomException error:
                    bytes = checked(bytes + 2L * (error.Name.Length + error.Message.Length + (error.Stack?.Length ?? 0)));
                    break;
                case SerializedRegExp regex:
                    bytes = checked(bytes + 2L * (regex.Source.Length + regex.Flags.Length));
                    break;
            }
        }
        return bytes;

        void Properties(List<SerializedProperty> properties)
        {
            foreach (var property in properties)
            {
                bytes = checked(bytes + 32 + 2L * property.Key.Length);
                pending.Push(property.Value);
            }
        }
    }
}
#endif
