#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.IndexedDb;

/// <summary>A database connection: https://w3c.github.io/IndexedDB/#database-interface.</summary>
internal sealed class JsIdbDatabase : JsEventTarget
{
    private Dictionary<string, ObjectStoreData> _schema;

    internal JsIdbDatabase(Engine engine, Realm realm, IndexedDbAgent agent, IndexedDbStore.Connection connection)
        : base(engine, realm)
    {
        Agent = agent;
        Connection = connection;
        Version = connection.Version;
        _schema = agent.Store.Snapshot(connection);
        _prototype = realm.Intrinsics.IndexedDb.Prototype("IDBDatabase");
        agent.Track(this);
    }

    internal IndexedDbAgent Agent { get; }
    internal IndexedDbStore.Connection Connection { get; }
    internal JsIdbTransaction? Upgrade;
    internal bool Closed { get; private set; }
    internal string Name => Connection.Database.Name;
    internal double Version { get; set; }
    internal Dictionary<string, ObjectStoreData> Schema => Upgrade?.Working ?? _schema;
    internal string[] Names() => Schema.Keys.Order(StringComparer.Ordinal).ToArray();

    internal void EndUpgrade(Dictionary<string, ObjectStoreData> schema)
    {
        _schema = schema;
        Upgrade = null;
    }

    internal JsIdbTransaction Transaction(string[] names, string mode, string durability)
    {
        if (Upgrade is not null) IndexedDbErrors.Throw(_realm, "InvalidStateError", "An upgrade transaction is running.");
        if (Closed) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The connection is closing.");
        if (names.Length == 0) IndexedDbErrors.Throw(_realm, "InvalidAccessError", "A transaction requires at least one object store.");
        var schema = Schema;
        foreach (var name in names)
        {
            if (!schema.ContainsKey(name)) IndexedDbErrors.Throw(_realm, "NotFoundError", "The object store does not exist.");
        }
        return new JsIdbTransaction(_engine, _realm, this, names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), mode, durability);
    }

    internal JsIdbTransaction RequireUpgrade()
    {
        if (Upgrade is not { Finished: false } transaction)
        {
            return IndexedDbErrors.Throw<JsIdbTransaction>(_realm, "InvalidStateError", "A versionchange transaction is required.");
        }
        transaction.RequireActive();
        return transaction;
    }

    internal JsIdbObjectStore CreateStore(string name, IndexedDbKeyPath? keyPath, bool autoIncrement)
    {
        var transaction = RequireUpgrade();
        if (transaction.Working.ContainsKey(name)) IndexedDbErrors.Throw(_realm, "ConstraintError", "The object store already exists.");
        if (autoIncrement && keyPath is not null && (keyPath.IsSequence || keyPath.Paths[0].Length == 0))
        {
            IndexedDbErrors.Throw(_realm, "InvalidAccessError", "Auto-increment requires a nonempty, noncompound key path.");
        }
        transaction.Working.Add(name, new ObjectStoreData(name, keyPath, autoIncrement));
        return transaction.ObjectStore(name);
    }

    internal void DeleteStore(string name)
    {
        var transaction = RequireUpgrade();
        if (!transaction.Working.Remove(name)) IndexedDbErrors.Throw(_realm, "NotFoundError", "The object store does not exist.");
    }

    internal void Close()
    {
        Closed = true;
        Agent.Store.Close(Connection);
        Agent.Forget(this);
    }
}
#endif
