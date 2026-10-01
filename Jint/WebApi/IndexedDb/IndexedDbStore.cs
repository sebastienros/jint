#if NET8_0_OR_GREATER
using System.Linq;
using System.Threading;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.IndexedDb;

/// <summary>Coordinates connections and transactions: https://w3c.github.io/IndexedDB/#transaction-scheduling.</summary>
/// <remarks>
/// Published data is immutable. Callbacks only post notifications; they never run script or read an engine.
/// A transaction copies its scoped stores on its own engine thread, then publishes them atomically here.
/// </remarks>
internal sealed class IndexedDbStore(long maxBytes = long.MaxValue)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Database> _databases = new(StringComparer.Ordinal);
    private long _bytes;

    internal OpenOperation Open(string name, double? version, bool delete, Action<Notice> notify)
    {
        lock (_gate)
        {
            if (!_databases.TryGetValue(name, out var database))
            {
                _databases.Add(name, database = new Database(name));
            }
            var operation = new OpenOperation(database, version, delete, notify);
            database.Opens.Enqueue(operation);
            ProcessOpens(database);
            return operation;
        }
    }

    internal DatabaseInfo[] Databases()
    {
        lock (_gate)
        {
            return _databases.Values.Where(static d => d.Version != 0)
                .Select(static d => new DatabaseInfo(d.Name, d.Version)).ToArray();
        }
    }

    internal void Acknowledge(OpenOperation operation, Connection connection)
    {
        lock (_gate)
        {
            operation.Awaiting.Remove(connection);
            ProcessOpens(operation.Database);
        }
    }

    internal void CompleteOpen(OpenOperation operation)
    {
        lock (_gate)
        {
            operation.Finished = true;
            ProcessOpens(operation.Database);
        }
    }

    private void ProcessOpens(Database database)
    {
        while (database.Opens.TryPeek(out var operation))
        {
            if (operation.Finished)
            {
                database.Opens.Dequeue();
                continue;
            }
            if (operation.Running) return;
            var version = operation.Delete ? 0 : operation.Version ?? (database.Version == 0 ? 1 : database.Version);
            if (!operation.Delete && version < database.Version)
            {
                operation.Running = true;
                operation.Notify(new Notice(NoticeKind.Error, operation, Error: "VersionError"));
                return;
            }
            if (operation.Delete || version > database.Version)
            {
                if (!operation.Notified)
                {
                    operation.Notified = true;
                    foreach (var connection in database.Connections)
                    {
                        if (connection.Closing) continue;
                        operation.Awaiting.Add(connection);
                        connection.Notify?.Invoke(new Notice(NoticeKind.VersionChange, operation, connection,
                            OldVersion: database.Version, NewVersion: operation.Delete ? null : version));
                    }
                }
                if (operation.Awaiting.Count != 0) return;
                if (database.Connections.Count != 0)
                {
                    if (!operation.Blocked)
                    {
                        operation.Blocked = true;
                        operation.Notify(new Notice(NoticeKind.Blocked, operation,
                            OldVersion: database.Version, NewVersion: operation.Delete ? null : version));
                    }
                    return;
                }
                operation.Running = true;
                if (operation.Delete)
                {
                    var old = database.Version;
                    _bytes -= database.Bytes;
                    database.Bytes = 0;
                    database.Stores = new(StringComparer.Ordinal);
                    database.Version = 0;
                    operation.Notify(new Notice(NoticeKind.Deleted, operation, OldVersion: old));
                }
                else
                {
                    var connection = new Connection(database, version, operation.Notify);
                    database.Connections.Add(connection);
                    operation.Connection = connection;
                    operation.Notify(new Notice(NoticeKind.Upgrade, operation, connection,
                        OldVersion: database.Version, NewVersion: version));
                }
                return;
            }
            else
            {
                operation.Running = true;
                var connection = new Connection(database, version, operation.Notify);
                database.Connections.Add(connection);
                operation.Connection = connection;
                operation.Notify(new Notice(NoticeKind.Opened, operation, connection));
                return;
            }
        }
        if (database.Version == 0 && database.Connections.Count == 0 && database.Transactions.Count == 0
            && _databases.TryGetValue(database.Name, out var current) && ReferenceEquals(current, database))
        {
            _databases.Remove(database.Name);
        }
    }

    internal Dictionary<string, ObjectStoreData> Snapshot(Connection connection)
    {
        lock (_gate) return new(connection.Database.Stores, StringComparer.Ordinal);
    }

    internal TransactionLease Begin(Connection connection, string[] scope, string mode,
        Action<Dictionary<string, ObjectStoreData>> ready)
    {
        lock (_gate)
        {
            var transaction = new TransactionLease(connection, scope, mode, ready);
            connection.Database.Transactions.Add(transaction);
            Schedule(connection.Database);
            return transaction;
        }
    }

    private static void Schedule(Database database)
    {
        var transactions = database.Transactions;
        for (var i = 0; i < transactions.Count; i++)
        {
            var candidate = transactions[i];
            if (candidate.Started) continue;
            var blocked = false;
            for (var j = 0; j < i; j++)
            {
                var earlier = transactions[j];
                if (candidate.Mode is "readonly" && earlier.Mode is "readonly") continue;
                if (candidate.Mode is "versionchange" || earlier.Mode is "versionchange"
                    || candidate.Scope.Any(name => System.Array.IndexOf(earlier.Scope, name) >= 0))
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked) continue;
            candidate.Started = true;
            var snapshot = candidate.Mode is "versionchange"
                ? new Dictionary<string, ObjectStoreData>(database.Stores, StringComparer.Ordinal)
                : candidate.Scope.ToDictionary(name => name, name => database.Stores[name], StringComparer.Ordinal);
            candidate.Ready(snapshot);
        }
    }

    /// <summary>Atomically publishes or discards a transaction: https://w3c.github.io/IndexedDB/#commit-transaction.</summary>
    internal bool Finish(TransactionLease transaction, Dictionary<string, ObjectStoreData>? changes, out long requested)
    {
        lock (_gate)
        {
            requested = _bytes;
            if (transaction.Finished) return true;
            var database = transaction.Connection.Database;
            if (changes is not null && transaction.Mode is not "readonly")
            {
                var next = transaction.Mode is "versionchange"
                    ? new Dictionary<string, ObjectStoreData>(changes, StringComparer.Ordinal)
                    : new Dictionary<string, ObjectStoreData>(database.Stores, StringComparer.Ordinal);
                if (transaction.Mode is not "versionchange")
                {
                    foreach (var name in transaction.Scope) next[name] = changes[name];
                }
                var bytes = 64L + 2L * database.Name.Length;
                foreach (var store in next.Values) bytes = checked(bytes + store.ByteSize);
                requested = checked(_bytes - database.Bytes + bytes);
                if (requested > maxBytes) return false;
                database.Stores = next;
                database.Bytes = bytes;
                _bytes = requested;
                if (transaction.Mode is "versionchange") database.Version = transaction.Connection.Version;
            }
            transaction.Finished = true;
            database.Transactions.Remove(transaction);
            Schedule(database);
            TryClose(transaction.Connection);
            return true;
        }
    }

    internal long MaxBytes => maxBytes;

    internal void Close(Connection connection)
    {
        lock (_gate)
        {
            connection.Closing = true;
            TryClose(connection);
        }
    }

    private void TryClose(Connection connection)
    {
        if (!connection.Closing || connection.Database.Transactions.Exists(t => t.Connection == connection)) return;
        var database = connection.Database;
        database.Connections.Remove(connection);
        connection.Notify = null;
        foreach (var operation in database.Opens) operation.Awaiting.Remove(connection);
        ProcessOpens(database);
    }

    internal enum NoticeKind { Opened, Upgrade, VersionChange, Blocked, Deleted, Error }

    internal sealed record Notice(NoticeKind Kind, OpenOperation Operation, Connection? Connection = null,
        double OldVersion = 0, double? NewVersion = null, string? Error = null);
    internal readonly record struct DatabaseInfo(string Name, double Version);

    internal sealed class Database(string name)
    {
        internal string Name { get; } = name;
        internal double Version;
        internal long Bytes;
        internal Dictionary<string, ObjectStoreData> Stores = new(StringComparer.Ordinal);
        internal List<Connection> Connections { get; } = [];
        internal List<TransactionLease> Transactions { get; } = [];
        internal Queue<OpenOperation> Opens { get; } = [];
    }

    internal sealed class Connection(Database database, double version, Action<Notice> notify)
    {
        internal Database Database { get; } = database;
        internal double Version { get; } = version;
        internal Action<Notice>? Notify = notify;
        internal bool Closing;
    }

    internal sealed class OpenOperation(Database database, double? version, bool delete, Action<Notice> notify)
    {
        internal Database Database { get; } = database;
        internal double? Version { get; } = version;
        internal bool Delete { get; } = delete;
        internal Action<Notice> Notify { get; } = notify;
        internal HashSet<Connection> Awaiting { get; } = [];
        internal Connection? Connection;
        internal bool Notified;
        internal bool Blocked;
        internal bool Running;
        internal bool Finished;
    }

    internal sealed class TransactionLease(Connection connection, string[] scope, string mode,
        Action<Dictionary<string, ObjectStoreData>> ready)
    {
        internal Connection Connection { get; } = connection;
        internal string[] Scope { get; } = scope;
        internal string Mode { get; } = mode;
        internal Action<Dictionary<string, ObjectStoreData>> Ready { get; } = ready;
        internal bool Started;
        internal bool Finished;
    }
}

/// <summary>Transaction-owned data, frozen on publication: https://w3c.github.io/IndexedDB/#object-store-construct.</summary>
internal sealed class ObjectStoreData(string name, IndexedDbKeyPath? keyPath, bool autoIncrement)
{
    internal Guid Id { get; private init; } = Guid.NewGuid();
    internal string Name = name;
    internal IndexedDbKeyPath? KeyPath { get; } = keyPath;
    internal bool AutoIncrement { get; } = autoIncrement;
    internal double Generator = 1;
    internal SortedDictionary<IndexedDbKey, StoredRecord> Records { get; } = new();
    internal Dictionary<string, IndexData> Indexes { get; } = new(StringComparer.Ordinal);
    internal long RecordBytes;
    internal long ByteSize => 128L + 2L * Name.Length + (KeyPath?.Paths.Sum(static p => 2L * p.Length) ?? 0)
        + RecordBytes + Indexes.Values.Sum(static i => i.ByteSize);

    internal ObjectStoreData Copy(Action check)
    {
        var result = new ObjectStoreData(Name, KeyPath, AutoIncrement) { Id = Id, Generator = Generator, RecordBytes = RecordBytes };
        var n = 0;
        foreach (var record in Records)
        {
            if ((++n & 255) == 0) check();
            result.Records.Add(record.Key, record.Value);
        }
        foreach (var index in Indexes) result.Indexes.Add(index.Key, index.Value.Copy(check));
        return result;
    }
}

internal sealed record StoredRecord(SerializationRecord Value, long Bytes);

/// <summary>Sorted index-key/primary-key pairs: https://w3c.github.io/IndexedDB/#index-construct.</summary>
internal sealed class IndexData(string name, IndexedDbKeyPath keyPath, bool unique, bool multiEntry)
{
    internal Guid Id { get; private init; } = Guid.NewGuid();
    internal string Name = name;
    internal IndexedDbKeyPath KeyPath { get; } = keyPath;
    internal bool Unique { get; } = unique;
    internal bool MultiEntry { get; } = multiEntry;
    internal SortedSet<IndexEntry> Entries { get; } = new();
    internal long EntryBytes;
    internal long ByteSize => 96L + 2L * Name.Length + KeyPath.Paths.Sum(static p => 2L * p.Length) + EntryBytes;

    internal IndexData Copy(Action check)
    {
        var result = new IndexData(Name, KeyPath, Unique, MultiEntry) { Id = Id, EntryBytes = EntryBytes };
        var n = 0;
        foreach (var entry in Entries)
        {
            if ((++n & 255) == 0) check();
            result.Entries.Add(entry);
        }
        return result;
    }
}

internal readonly record struct IndexEntry(IndexedDbKey Key, IndexedDbKey PrimaryKey) : IComparable<IndexEntry>
{
    public int CompareTo(IndexEntry other)
    {
        var order = Key.CompareTo(other.Key);
        return order == 0 ? PrimaryKey.CompareTo(other.PrimaryKey) : order;
    }
}
#endif
