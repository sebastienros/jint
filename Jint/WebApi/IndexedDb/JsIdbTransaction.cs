#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.IndexedDb;

/// <summary>Transaction activity, requests and atomic publication: https://w3c.github.io/IndexedDB/#transaction-lifetime.</summary>
internal sealed class JsIdbTransaction : JsEventTarget
{
    private readonly Queue<Operation> _pending = new();
    private readonly Dictionary<Guid, JsIdbObjectStore> _handles = new();
    private readonly IndexedDbStore.TransactionLease _lease;
    private readonly Action<bool>? _ended;
    private readonly Dictionary<string, ObjectStoreData> _original;
    private readonly double _originalVersion;
    private bool _started;
    private bool _scheduled;
    private bool _dispatching;
    private bool _committing;

    internal JsIdbTransaction(Engine engine, Realm realm, JsIdbDatabase database, string[] names, string mode, string durability,
        Action? started = null, Action<bool>? ended = null) : base(engine, realm)
    {
        Database = database;
        Mode = mode;
        Durability = durability;
        Scope = names;
        _ended = ended;
        _prototype = realm.Intrinsics.IndexedDb.Prototype("IDBTransaction");
        _original = database.Agent.Store.Snapshot(database.Connection);
        _originalVersion = mode is "versionchange" ? database.Connection.Database.Version : database.Version;
        Working = new(_original, StringComparer.Ordinal);
        Agent.Track(this);
        var ready = Agent.Poster<Dictionary<string, ObjectStoreData>>(snapshot =>
        {
            if (Finished) return;
            using var realmScope = new RealmScope(engine, realm);
            Working = mode is "readonly" ? snapshot : snapshot.ToDictionary(static e => e.Key,
                e => e.Value.Copy(engine.Constraints.Check), StringComparer.Ordinal);
            _started = true;
            started?.Invoke();
            Pump();
        });
        _lease = Agent.Store.Begin(database.Connection, names, mode, ready);
    }

    internal JsIdbDatabase Database { get; }
    internal IndexedDbAgent Agent => Database.Agent;
    internal string[] Scope { get; }
    internal string Mode { get; }
    internal string Durability { get; }
    internal bool Active { get; set; } = true;
    internal bool Finished { get; private set; }
    internal JsValue Error { get; private set; } = Null;
    internal Dictionary<string, ObjectStoreData> Working { get; private set; }
    internal override bool HasEventPath => true;
    internal override JsEventTarget? GetParent(JsEvent ev) => Database;
    internal string[] Names() => Mode is "versionchange" ? Working.Keys.Order(StringComparer.Ordinal).ToArray() : Scope;

    internal void RequireActive(bool write = false)
    {
        if (!Active || Finished || _committing) IndexedDbErrors.Throw(_realm, "TransactionInactiveError", "The transaction is not active.");
        if (write && Mode is "readonly") IndexedDbErrors.Throw(_realm, "ReadOnlyError", "The transaction is read-only.");
    }

    internal JsIdbObjectStore ObjectStore(string name)
    {
        if (Finished) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The transaction is finished.");
        if ((Mode is not "versionchange" && System.Array.IndexOf(Scope, name) < 0) || !Working.TryGetValue(name, out var data))
        {
            return IndexedDbErrors.Throw<JsIdbObjectStore>(_realm, "NotFoundError", "The object store is not in this transaction.");
        }
        if (!_handles.TryGetValue(data.Id, out var handle))
        {
            _handles.Add(data.Id, handle = new JsIdbObjectStore(_engine, _realm, this, data));
        }
        return handle;
    }

    internal JsIdbRequest Queue(JsValue source, Func<JsValue> execute, JsIdbRequest? request = null)
    {
        RequireActive();
        request ??= new JsIdbRequest(_engine, _realm, source, this);
        request.Done = false;
        request.Result = Undefined;
        request.Error = Null;
        _pending.Enqueue(new Operation(request, execute));
        Pump();
        return request;
    }

    internal void QueueSchema(Action execute)
    {
        _pending.Enqueue(new Operation(null, () => { execute(); return Undefined; }));
        Pump();
    }

    private void Pump()
    {
        if (!_started || _scheduled || _dispatching || Finished) return;
        if (_pending.Count == 0)
        {
            if (!Active || _committing) CommitCore();
            return;
        }
        _scheduled = true;
        Agent.Post(() =>
        {
            _scheduled = false;
            if (Finished) return;
            var operation = _pending.Dequeue();
            using var scope = new RealmScope(_engine, _realm);
            JsValue result;
            try { result = operation.Execute(); }
            catch (JavaScriptException exception)
            {
                if (operation.Request is { } failed)
                {
                    Dispatch(failed, failed.Settle(Undefined, exception.Error));
                }
                else Abort(exception.Error);
                Pump();
                return;
            }
            if (operation.Request is { } request) Dispatch(request, request.Settle(result));
            Pump();
        });
    }

    internal void Dispatch(JsIdbRequest request, JsEvent ev)
    {
        _dispatching = true;
        Active = !_committing;
        try
        {
            request.DispatchEvent(ev);
            if (!Finished && ((ev.ListenersThrew && !_committing) || (request.Error != Null && (!ev.CanceledFlag || _committing))))
            {
                Abort(ev.ListenersThrew
                    ? _realm.Intrinsics.DomException.CreateException("AbortError", "A request listener threw an exception.")
                    : request.Error);
            }
        }
        catch (JavaScriptException)
        {
            // https://w3c.github.io/IndexedDB/#fire-a-success-event and #fire-an-error-event:
            // a throwing listener aborts only an active transaction, even without a diagnostics sink.
            if (!Finished && !_committing)
            {
                Abort(_realm.Intrinsics.DomException.CreateException("AbortError", "A request listener threw an exception."));
            }
            throw;
        }
        finally
        {
            Active = false;
            _dispatching = false;
        }
    }

    internal void Cleanup()
    {
        if (_dispatching || Finished) return;
        Active = false;
        Pump();
    }

    internal void Commit()
    {
        if (!Active || Finished || _committing) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The transaction cannot be committed.");
        _committing = true;
        Active = false;
        Pump();
    }

    private void CommitCore()
    {
        if (Finished || _scheduled) return;
        _committing = true;
        _scheduled = true;
        Agent.Post(() =>
        {
            _scheduled = false;
            if (Finished) return;
            if (!Agent.Store.Finish(_lease, Working, out _))
            {
                Abort(_realm.Intrinsics.DomException.CreateException("QuotaExceededError", "The IndexedDB partition quota was exceeded."));
                return;
            }
            Finished = true;
            Agent.Forget(this);
            if (Mode is "versionchange") Database.EndUpgrade(Working);
            try { DispatchEvent(Trusted("complete")); }
            finally { _ended?.Invoke(true); }
        });
    }

    internal void Abort(JsValue? error = null)
    {
        if (Finished) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The transaction has finished.");
        Finished = true;
        Active = false;
        Error = error ?? Null;
        Working = new(_original, StringComparer.Ordinal);
        if (Mode is "versionchange") Database.Version = _originalVersion;
        var requests = _pending.ToArray();
        _pending.Clear();
        foreach (var operation in requests)
        {
            if (operation.Request is not { } request || request.Done) continue;
            Agent.Post(() =>
            {
                var ev = request.Settle(Undefined, _realm.Intrinsics.DomException.CreateException("AbortError", "The transaction was aborted."));
                request.DispatchEvent(ev);
            });
        }
        Agent.Post(() =>
        {
            try
            {
                Agent.Store.Finish(_lease, null, out _);
                Agent.Forget(this);
                if (Mode is "versionchange") Database.EndUpgrade(Working);
                DispatchEvent(Trusted("abort", bubbles: true));
            }
            finally
            {
                Agent.Store.Finish(_lease, null, out _);
                Agent.Forget(this);
                _ended?.Invoke(false);
            }
        });
    }

    internal void Abandon()
    {
        Finished = true;
        Active = false;
        _pending.Clear();
        Agent.Store.Finish(_lease, null, out _);
        Agent.Forget(this);
    }

    private JsEvent Trusted(string type, bool bubbles = false) => new(_engine, JsString.Create(type),
        new EventInit(bubbles, false, false), EventConstructor.TimeStampNow(_engine))
    {
        _prototype = _realm.Intrinsics.Event.PrototypeObject,
        IsTrusted = true,
    };

    private sealed record Operation(JsIdbRequest? Request, Func<JsValue> Execute);
}
#endif
