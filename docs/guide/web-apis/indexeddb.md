# IndexedDB

IndexedDB requires .NET 8 or later and is an explicit grant, separate from `WebApiFeatures.Default`:

```csharp
using var engine = new Engine(options =>
    options.UseWebApis(WebApiFeatures.IndexedDb));

engine.Execute("""
    const opening = indexedDB.open("example", 1);
    opening.onupgradeneeded = () =>
        opening.result.createObjectStore("items", { keyPath: "id" });
    opening.onsuccess = () => {
        const db = opening.result;
        const tx = db.transaction("items", "readwrite");
        tx.objectStore("items").put({ id: 1, title: "Stored" });
        tx.oncomplete = () => db.close();
        tx.onabort = () => { globalThis.storageError = tx.error; };
    };
    """);
engine.Tasks.ProcessTasks();
```

The flag also enables events, DOMException and structured cloning. It grants no network access. Each ordinary
engine has its own in-memory store with a 50 MiB retained-data quota. Stored data survives a
global snapshot restore; live connections and transactions do not. Private data is retained with the engine;
dispose it to release connections, and release references to it to reclaim that data.
Set `Options.WebApi.IndexedDb.MaxBytes` before first IndexedDB use (also supported in the
`Engine.WebApi.Enable` configuration callback). The nonnegative quota covers committed serialized values,
keys, index entries and metadata across all databases on that engine. A commit exceeding it aborts with
`QuotaExceededError`; failed commits publish no changes. Deletion releases the charge. Zero refuses database
creation (the aborted upgrade's open request reports `AbortError`); `long.MaxValue` explicitly opts into practically unlimited retained data. The quota and its charge
survive global snapshot restores and repeated host entries, independently of `LimitMemory`.

```csharp
using var engine = new Engine(options =>
{
    options.WebApi.IndexedDb.MaxBytes = 5 * 1024 * 1024;
    options.UseWebApis(WebApiFeatures.IndexedDb);
});
```

Changing the option after the private store has been created does not resize it. Sharing `Options` shares
configuration, never the private data. There is no public IndexedDB storage-provider API. This quota bounds
retained data, not peak allocations during cloning or an in-flight transaction; use execution and memory
budgets as well.

## Requests, transactions and values

The implementation supplies `IDBFactory`, requests and open requests, database connections, transactions,
object stores, indexes, cursors and value cursors, key ranges, version-change events, and `DOMStringList`.
It implements the [IndexedDB algorithms](https://w3c.github.io/IndexedDB/) for numeric, date, string, binary
and array keys, inline and compound key paths, auto-increment, unique and multi-entry indexes, range queries,
cursor directions and mutation, database upgrades and deletion.

Requests deliver events as tasks on the owning engine. `Evaluate`/`Execute` drain available work; hosts using
other entry points or sharing storage through the browser must continue pumping. No storage thread runs script.
An unpumped connection can block an upgrade in another page; close connections when finished, and handle
`versionchange` by closing a connection that no longer needs to stay open.

A transaction is active during its creating task and request-event dispatch, including their microtasks.
Do not wait for a timer or network operation and then reuse it. When its requests finish it commits
automatically; `commit()` disallows further requests. Request errors bubble through transaction and database.
Calling `preventDefault()` on a request error suppresses the automatic abort unless the transaction is already
committing. An uncaught request-listener exception aborts an active transaction; the ordinary diagnostics-sink
rules still determine whether that exception is reported or propagates to the host. On a sinkless engine,
resume pumping after catching the exception to deliver pending abort/request-error events and settle an
aborted upgrade's open request. Unrelated connections and queued opens remain usable. Constraint failures
still propagate and release the engine's IndexedDB activity.

Values are structured-cloned when `put`/`add` is called, not when its request executes. Reads produce new clones,
including fresh mutable buffer storage. Aborts discard changes to records, schema, indexes and key generators.
Readonly transactions can overlap; overlapping readwrite scopes are serialized across connections and threads.
Disjoint writers publish only their own stores.

## Jint.Browser

Pages and dedicated workers receive IndexedDB automatically, including plain HTTP origins. Storage is shared
by origin within one `BrowserContext`, survives same-origin navigation, and is isolated from other origins and
contexts. It remains available with a custom `StoragePartitionProvider`: the IndexedDB partition is
context-owned, not an extension of that public provider.

`BrowserOptions.MaxIndexedDbBytes` defaults to 50 MiB per origin. Accounting covers committed serialized values,
keys, index entries and metadata across all databases. A commit that exceeds the quota aborts with
`QuotaExceededError`; deleting data/databases releases its charge. Zero refuses database creation. This is a
retained-data quota, not a bound on peak allocations while cloning values or holding transaction state;
use the browser's memory and task budgets as well.

Opaque origins still expose the interface. `open()` and `deleteDatabase()` throw `SecurityError`, and
`databases()` rejects with it. Creator-inherited `about:blank` uses the creator's origin.

## Deliberate limits

- Storage is in memory only: no persistence to disk, eviction policy, or persistent-storage permission.
- `durability` is validated and returned, but has no disk-flush behavior.
- No IndexedDB CDP domain or `Storage.clearDataForOrigin` integration.
- IDB 3.1 additions such as `getAllRecords()` and the newer `getAll` options overload are not implemented.
- Writable transactions fork store/index metadata and share persistent record/index trees; changed tree paths allocate additional memory.
- The upstream WPT IndexedDB directory is not yet part of the pinned vendored corpus. Repository tests cover
  the implementation, but this is not a claim of full IndexedDB WPT conformance.
