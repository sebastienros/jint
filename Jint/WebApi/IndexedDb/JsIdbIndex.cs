#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.IndexedDb;

/// <summary>A transaction-scoped index: https://w3c.github.io/IndexedDB/#index-interface.</summary>
internal sealed class JsIdbIndex : ObjectInstance
{
    private readonly Realm _realm;
    private IndexData _data;
    private JsValue? _keyPath;

    internal JsIdbIndex(Engine engine, Realm realm, JsIdbObjectStore store, IndexData data) : base(engine)
    {
        _realm = realm;
        Store = store;
        _data = data;
        _prototype = realm.Intrinsics.IndexedDb.Prototype("IDBIndex");
    }

    internal JsIdbObjectStore Store { get; }
    internal JsValue KeyPath => _keyPath ??= Data.KeyPath.ToValue(_engine);
    internal IndexData Data
    {
        get
        {
            foreach (var index in Store.Data.Indexes.Values)
            {
                if (index.Id == _data.Id) return _data = index;
            }
            return _data;
        }
    }

    internal IndexData RequireData(bool active = true)
    {
        Store.RequireData(active: false);
        var data = Data;
        if (!Store.Data.Indexes.Values.Any(i => i.Id == data.Id)) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The index was deleted.");
        if (active) Store.Transaction.RequireActive();
        return data;
    }

    internal void Rename(string name)
    {
        var data = RequireData(active: false);
        Store.Transaction.Database.RequireUpgrade();
        if (string.Equals(name, data.Name, StringComparison.Ordinal)) return;
        if (Store.Data.Indexes.ContainsKey(name)) IndexedDbErrors.Throw(_realm, "ConstraintError", "The index name is already used.");
        Store.Data.Indexes.Remove(data.Name);
        data.Name = name;
        Store.Data.Indexes.Add(name, data);
    }
}
#endif
