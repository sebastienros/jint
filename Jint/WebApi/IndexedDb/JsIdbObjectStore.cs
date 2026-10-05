#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.IndexedDb;

/// <summary>A transaction-scoped object store: https://w3c.github.io/IndexedDB/#object-store-interface.</summary>
internal sealed class JsIdbObjectStore : ObjectInstance
{
    private readonly Realm _realm;
    private ObjectStoreData _data;
    private JsValue? _keyPath;
    private readonly Dictionary<Guid, JsIdbIndex> _indexes = new();

    internal JsIdbObjectStore(Engine engine, Realm realm, JsIdbTransaction transaction, ObjectStoreData data) : base(engine)
    {
        _realm = realm;
        Transaction = transaction;
        _data = data;
        _prototype = realm.Intrinsics.IndexedDb.Prototype("IDBObjectStore");
    }

    internal JsIdbTransaction Transaction { get; }
    internal JsValue KeyPath => _keyPath ??= Data.KeyPath?.ToValue(_engine) ?? Null;
    internal ObjectStoreData Data
    {
        get
        {
            foreach (var store in Transaction.Working.Values)
            {
                if (store.Id == _data.Id) return _data = store;
            }
            return _data;
        }
    }

    internal ObjectStoreData RequireData(bool active = true, bool write = false)
    {
        var data = Data;
        if (!Transaction.Working.Values.Any(s => s.Id == data.Id))
        {
            IndexedDbErrors.Throw(_realm, "InvalidStateError", "The object store was deleted.");
        }
        if (active) Transaction.RequireActive(write);
        return data;
    }

    internal void Rename(string name)
    {
        var data = RequireData(active: false);
        Transaction.Database.RequireUpgrade();
        if (string.Equals(name, data.Name, StringComparison.Ordinal)) return;
        if (Transaction.Working.ContainsKey(name)) IndexedDbErrors.Throw(_realm, "ConstraintError", "The object store name is already used.");
        Transaction.Working.Remove(data.Name);
        data.Name = name;
        Transaction.Working.Add(name, data);
    }

    internal JsIdbRequest Put(JsValue value, JsValue keyValue, bool overwrite, JsIdbCursor? cursor = null)
    {
        var data = RequireData(write: true);
        var keyProvided = !keyValue.IsUndefined();
        if (cursor is null && data.KeyPath is not null && keyProvided)
        {
            IndexedDbErrors.Throw(_realm, "DataError", "An explicit key cannot be used with an inline key path.");
        }
        IndexedDbKey? key = cursor?.Position?.PrimaryKey;
        if (key is null && keyProvided) key = IndexedDbKeyConverter.Require(_engine, _realm, keyValue);
        if (data.KeyPath is null && key is null && !data.AutoIncrement)
        {
            IndexedDbErrors.Throw(_realm, "DataError", "An out-of-line key is required.");
        }
        SerializationRecord record;
        Transaction.Active = false;
        try { record = new StructuredSerializer(_engine, _realm).Serialize(value, null); }
        finally { Transaction.Active = true; }
        Transaction.RequireActive(write: true);
        if (data.KeyPath is not null)
        {
            if (data.KeyPath.TryEvaluate(record.Root, out var extracted))
            {
                var extractedKey = IndexedDbKeyPath.ToKey(extracted, _engine.Constraints.Check);
                if (extractedKey is null || (key is not null && extractedKey.CompareTo(key) != 0))
                {
                    IndexedDbErrors.Throw(_realm, "DataError", "The inline key is invalid or differs from the cursor key.");
                }
                key = extractedKey;
            }
            else if (cursor is not null || !data.AutoIncrement || !data.KeyPath.Inject(record.Root, 0, checkOnly: true))
            {
                IndexedDbErrors.Throw(_realm, "DataError", "The inline key is missing and cannot be generated.");
            }
        }
        var source = cursor is null ? (JsValue) this : cursor;
        var indexes = CaptureIndexes();
        return Transaction.Queue(source, () => Store(record, key, overwrite, indexes));
    }

    // Schema changes take effect on handles immediately, but queued requests retain their earlier schema.
    private IndexData[]? CaptureIndexes()
        => Transaction.Mode is "versionchange" ? Data.Indexes.Values.ToArray() : null;

    private JsValue Store(SerializationRecord record, IndexedDbKey? key, bool overwrite, IndexData[]? queuedIndexes)
    {
        var data = Data;
        var indexes = queuedIndexes ?? (IEnumerable<IndexData>) data.Indexes.Values;
        var generator = data.Generator;
        if (key is null)
        {
            if (generator > 9007199254740992d)
            {
                IndexedDbErrors.Throw(_realm, "ConstraintError", "The key generator is exhausted.");
            }
            key = IndexedDbKey.Numeric(generator);
            generator = generator == 9007199254740992d ? 9007199254740994d : generator + 1;
            data.KeyPath?.Inject(record.Root, key.Number, checkOnly: false);
        }
        else if (data.AutoIncrement && key.Kind == IndexedDbKey.KeyKind.Number && key.Number >= generator)
        {
            generator = key.Number >= 9007199254740992d ? 9007199254740994d : System.Math.Floor(key.Number) + 1;
        }
        if (!overwrite && data.Records.ContainsKey(key))
        {
            IndexedDbErrors.Throw(_realm, "ConstraintError", "A record with this key already exists.");
        }
        var indexKeys = new List<(IndexData Index, SortedSet<IndexedDbKey> Keys)>();
        foreach (var index in indexes)
        {
            _engine.Constraints.Check();
            var keys = index.KeyPath.IndexKeys(record.Root, index.MultiEntry, _engine.Constraints.Check);
            if (index.Unique)
            {
                foreach (var candidate in keys)
                {
                    _engine.Constraints.Check();
                    if (index.Entries.TryGetValue(new IndexEntry(candidate, key), out var entry)
                        && entry.PrimaryKey.CompareTo(key) != 0)
                    {
                        IndexedDbErrors.Throw(_realm, "ConstraintError", "The index requires unique keys.");
                    }
                }
            }
            indexKeys.Add((index, keys));
        }
        var size = checked(IndexedDbSize.Record(in record, _engine.Constraints.Check) + IndexedDbSize.Key(key) + 48);
        RemoveRecord(data, key, indexes);
        data.Records = data.Records.SetItem(key, new StoredRecord(record, size));
        data.RecordBytes = checked(data.RecordBytes + size);
        data.Generator = generator;
        foreach (var (index, keys) in indexKeys)
        {
            foreach (var indexKey in keys)
            {
                _engine.Constraints.Check();
                index.Entries = index.Entries.Add(new IndexEntry(indexKey, key));
                index.EntryBytes = checked(index.EntryBytes + IndexedDbSize.Key(indexKey) + IndexedDbSize.Key(key) + 32);
            }
        }
        return IndexedDbKeyConverter.ToValue(_engine, _realm, key);
    }

    private void RemoveRecord(ObjectStoreData data, IndexedDbKey key, IEnumerable<IndexData> indexes)
    {
        if (!data.Records.TryGetValue(key, out var previous)) return;
        data.Records = data.Records.Remove(key);
        data.RecordBytes -= previous.Bytes;
        foreach (var index in indexes)
        {
            _engine.Constraints.Check();
            // https://w3c.github.io/IndexedDB/#object-store-deletion-operation:
            // the previous value determines exactly which index records belong to this key.
            foreach (var indexKey in index.KeyPath.IndexKeys(previous.Value.Root, index.MultiEntry, _engine.Constraints.Check))
            {
                _engine.Constraints.Check();
                var entry = new IndexEntry(indexKey, key);
                if (index.Entries.TryGetValue(entry, out var existing)
                    && existing.PrimaryKey.CompareTo(key) == 0)
                {
                    index.Entries = index.Entries.Remove(entry);
                    index.EntryBytes -= IndexedDbSize.Key(existing.Key) + IndexedDbSize.Key(existing.PrimaryKey) + 32;
                }
            }
        }
    }

    internal JsIdbRequest Delete(JsIdbKeyRange? range, JsIdbCursor? cursor = null)
    {
        RequireData(write: true);
        var indexes = CaptureIndexes();
        return Transaction.Queue(cursor is null ? this : cursor, () =>
        {
            var data = Data;
            var currentIndexes = indexes ?? (IEnumerable<IndexData>) data.Indexes.Values;
            _engine.Constraints.Check();
            if (range is { Lower: { } lower, Upper: { } upper, LowerOpen: false, UpperOpen: false }
                && lower.CompareTo(upper) == 0)
            {
                RemoveRecord(data, lower, currentIndexes);
                return Undefined;
            }
            var keys = new List<IndexedDbKey>();
            foreach (var key in data.Records.Keys)
            {
                _engine.Constraints.Check();
                if (range is null || range.Contains(key)) keys.Add(key);
            }
            foreach (var key in keys)
            {
                _engine.Constraints.Check();
                RemoveRecord(data, key, currentIndexes);
            }
            return Undefined;
        });
    }

    internal JsIdbRequest Clear()
    {
        RequireData(write: true);
        var indexes = CaptureIndexes();
        return Transaction.Queue(this, () =>
        {
            var data = Data;
            data.Records = data.Records.Clear();
            data.RecordBytes = 0;
            foreach (var index in indexes ?? (IEnumerable<IndexData>) data.Indexes.Values)
            {
                index.Entries = index.Entries.Clear();
                index.EntryBytes = 0;
            }
            return Undefined;
        });
    }

    internal JsIdbIndex Index(string name)
    {
        var data = RequireData(active: false);
        if (Transaction.Finished) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The transaction is finished.");
        if (!data.Indexes.TryGetValue(name, out var index)) return IndexedDbErrors.Throw<JsIdbIndex>(_realm, "NotFoundError", "The index does not exist.");
        if (!_indexes.TryGetValue(index.Id, out var handle))
        {
            _indexes.Add(index.Id, handle = new JsIdbIndex(_engine, _realm, this, index));
        }
        return handle;
    }

    internal JsIdbIndex CreateIndex(string name, IndexedDbKeyPath path, bool unique, bool multiEntry)
    {
        var data = RequireData(active: false);
        Transaction.Database.RequireUpgrade();
        if (data.Indexes.ContainsKey(name)) IndexedDbErrors.Throw(_realm, "ConstraintError", "The index already exists.");
        if (multiEntry && path.IsSequence) IndexedDbErrors.Throw(_realm, "InvalidAccessError", "A multiEntry index cannot use a compound key path.");
        var index = new IndexData(name, path, unique, multiEntry);
        data.Indexes.Add(name, index);
        Transaction.QueueSchema(() =>
        {
            var current = Data;
            index.Entries = index.Entries.Clear();
            index.EntryBytes = 0;
            foreach (var record in current.Records)
            {
                _engine.Constraints.Check();
                foreach (var key in path.IndexKeys(record.Value.Value.Root, multiEntry, _engine.Constraints.Check))
                {
                    _engine.Constraints.Check();
                    if (unique && index.Entries.Contains(new IndexEntry(key, record.Key)))
                    {
                        IndexedDbErrors.Throw(_realm, "ConstraintError", "Existing records violate the unique index.");
                    }
                    index.Entries = index.Entries.Add(new IndexEntry(key, record.Key));
                    index.EntryBytes = checked(index.EntryBytes + IndexedDbSize.Key(key) + IndexedDbSize.Key(record.Key) + 32);
                }
            }
        });
        return Index(name);
    }

    internal void DeleteIndex(string name)
    {
        var data = RequireData(active: false);
        Transaction.Database.RequireUpgrade();
        if (!data.Indexes.Remove(name)) IndexedDbErrors.Throw(_realm, "NotFoundError", "The index does not exist.");
    }

    internal IEnumerable<IndexEntry> Entries(JsIdbIndex? index, bool reverse)
    {
        if (index is not null)
        {
            var entries = index.Data.Entries;
            return reverse ? entries.Reverse() : entries;
        }
        var keys = reverse ? Data.Records.Keys.Reverse() : Data.Records.Keys;
        return keys.Select(static key => new IndexEntry(key, key));
    }

    internal JsValue ReadValue(IndexedDbKey key)
    {
        var record = Data.Records[key].Value;
        return new StructuredDeserializer(_engine, _realm, sharedRecord: true).Deserialize(in record);
    }

    internal JsIdbRequest Query(JsIdbIndex? index, JsIdbKeyRange? range, string operation, uint count)
    {
        RequireData();
        index?.RequireData();
        return Transaction.Queue(index is null ? this : index, () =>
        {
            var values = new List<JsValue>();
            double total = 0;
            foreach (var entry in Entries(index, reverse: false))
            {
                _engine.Constraints.Check();
                if (range is not null && !range.Contains(entry.Key)) continue;
                if (operation is "count") { total++; continue; }
                var value = operation is "getKey" or "getAllKeys"
                    ? IndexedDbKeyConverter.ToValue(_engine, _realm, entry.PrimaryKey) : ReadValue(entry.PrimaryKey);
                if (operation is "get" or "getKey") return value;
                values.Add(value);
                if (count != 0 && values.Count >= count) break;
            }
            return operation switch
            {
                "count" => JsNumber.Create(total),
                "get" or "getKey" => Undefined,
                _ => new JsArray(_engine, values.ToArray()),
            };
        });
    }

    internal JsIdbRequest OpenCursor(JsIdbIndex? index, JsIdbKeyRange? range, string direction, bool keysOnly)
    {
        RequireData();
        index?.RequireData();
        var source = index is null ? (JsValue) this : index;
        var request = new JsIdbRequest(_engine, _realm, source, Transaction);
        var cursor = new JsIdbCursor(_engine, _realm, this, index, request, range, direction, keysOnly);
        return Transaction.Queue(source, () => cursor.Move(1, null, null), request);
    }
}
#endif
