#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.IndexedDb;

/// <summary>The origin's factory: https://w3c.github.io/IndexedDB/#factory-interface.</summary>
internal sealed class JsIdbFactory : ObjectInstance
{
    private readonly Realm _realm;
    internal IndexedDbAgent Agent => _engine._webApi!.IndexedDb;

    internal JsIdbFactory(Engine engine, Realm realm) : base(engine)
    {
        _realm = realm;
        _prototype = realm.Intrinsics.IndexedDb.Prototype("IDBFactory");
    }

    internal JsIdbOpenDbRequest Open(string name, double? version, bool delete)
    {
        if (Agent.OpaqueOrigin) IndexedDbErrors.Throw(_realm, "SecurityError", "The origin cannot access IndexedDB.");
        var request = new JsIdbOpenDbRequest(_engine, _realm);
        var post = Agent.Poster<IndexedDbStore.Notice>(notice => Receive(request, notice));
        var operation = Agent.Store.Open(name, version, delete, post);
        Agent.Track(operation);
        return request;
    }

    private void Receive(JsIdbOpenDbRequest request, IndexedDbStore.Notice notice)
    {
        using var scope = new RealmScope(_engine, _realm);
        var operation = notice.Operation;
        if (notice.Kind == IndexedDbStore.NoticeKind.VersionChange)
        {
            try
            {
                if (request.Database is { Closed: false } db)
                {
                    db.DispatchEvent(JsIdbVersionChangeEvent.Trusted(_engine, _realm, "versionchange", notice.OldVersion, notice.NewVersion));
                }
            }
            finally
            {
                Agent.Store.Acknowledge(operation, notice.Connection!);
            }
            return;
        }
        if (operation.Finished) return;
        switch (notice.Kind)
        {
            case IndexedDbStore.NoticeKind.Blocked:
                request.DispatchEvent(JsIdbVersionChangeEvent.Trusted(_engine, _realm, "blocked", notice.OldVersion, notice.NewVersion));
                break;
            case IndexedDbStore.NoticeKind.Error:
                Fail(request, operation, notice.Error!);
                break;
            case IndexedDbStore.NoticeKind.Deleted:
                request.Settle(Undefined);
                try
                {
                    request.DispatchEvent(JsIdbVersionChangeEvent.Trusted(_engine, _realm, "success", notice.OldVersion, null));
                }
                finally { Agent.Complete(operation); }
                break;
            case IndexedDbStore.NoticeKind.Opened:
                request.Database = new JsIdbDatabase(_engine, _realm, Agent, notice.Connection!);
                Succeed(request, operation);
                break;
            case IndexedDbStore.NoticeKind.Upgrade:
                var database = new JsIdbDatabase(_engine, _realm, Agent, notice.Connection!);
                request.Database = database;
                var transaction = new JsIdbTransaction(_engine, _realm, database,
                    database.Names(), "versionchange", "default",
                    started: () =>
                    {
                        request.Done = true;
                        request.Result = database;
                        var ev = JsIdbVersionChangeEvent.Trusted(_engine, _realm, "upgradeneeded", notice.OldVersion, notice.NewVersion);
                        database.Upgrade!.Dispatch(request, ev);
                    },
                    ended: committed =>
                    {
                        request.Transaction = null;
                        database.Upgrade = null;
                        if (!committed)
                        {
                            request.Result = Undefined;
                            request.Done = false;
                        }
                        Agent.Post(() =>
                        {
                            if (committed && !database.Closed) Succeed(request, operation);
                            else
                            {
                                if (!committed) database.Version = notice.OldVersion;
                                database.Close();
                                Fail(request, operation, "AbortError");
                            }
                        });
                    });
                database.Upgrade = transaction;
                request.Transaction = transaction;
                break;
        }
    }

    private void Succeed(JsIdbOpenDbRequest request, IndexedDbStore.OpenOperation operation)
    {
        var ev = request.Settle(request.Database!);
        try { request.DispatchEvent(ev); }
        finally { Agent.Complete(operation); }
    }

    private void Fail(JsIdbOpenDbRequest request, IndexedDbStore.OpenOperation operation, string error)
    {
        var ev = request.Settle(Undefined, _realm.Intrinsics.DomException.CreateException(error, "The database operation failed."));
        try { request.DispatchEvent(ev); }
        finally { Agent.Complete(operation); }
    }

    internal JsValue Databases()
    {
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        if (Agent.OpaqueOrigin)
        {
            capability.Reject(_realm.Intrinsics.DomException.CreateException("SecurityError", "The origin cannot access IndexedDB."));
            return capability.PromiseInstance;
        }
        Agent.Post(() =>
        {
            var result = Agent.Store.Databases().Select(info =>
            {
                var item = new JsObject(_engine);
                item.CreateDataPropertyOrThrow("name", JsString.Create(info.Name));
                item.CreateDataPropertyOrThrow("version", JsNumber.Create(info.Version));
                return (JsValue) item;
            }).ToArray();
            capability.Resolve(new JsArray(_engine, result));
        });
        return capability.PromiseInstance;
    }
}
#endif
