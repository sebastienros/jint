#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.IndexedDb;

/// <summary>A request and its event path: https://w3c.github.io/IndexedDB/#request-construct.</summary>
internal class JsIdbRequest : JsEventTarget
{
    internal JsIdbRequest(Engine engine, Realm realm, JsValue source, JsIdbTransaction? transaction, bool open = false)
        : base(engine, realm)
    {
        Source = source;
        Transaction = transaction;
        _prototype = realm.Intrinsics.IndexedDb.Prototype(open ? "IDBOpenDBRequest" : "IDBRequest");
    }

    internal Realm Realm => _realm;
    internal JsValue Source { get; }
    internal JsIdbTransaction? Transaction { get; set; }
    internal bool Done { get; set; }
    internal JsValue Result { get; set; } = Undefined;
    internal JsValue Error { get; set; } = Null;
    internal override bool HasEventPath => Transaction is not null;
    internal override JsEventTarget? GetParent(JsEvent ev) => Transaction;

    internal JsValue ReadResult()
    {
        RequireDone();
        return Result;
    }

    internal JsValue ReadError()
    {
        RequireDone();
        return Error;
    }

    private void RequireDone()
    {
        if (!Done) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The request has not completed.");
    }

    internal JsEvent Settle(JsValue result, JsValue? error = null)
    {
        Done = true;
        Result = result;
        Error = error ?? Null;
        var failed = error is not null;
        return new JsEvent(_engine, new JsString(failed ? "error" : "success"),
            new EventInit(failed, failed, false), EventConstructor.TimeStampNow(_engine))
        {
            _prototype = _realm.Intrinsics.Event.PrototypeObject,
            IsTrusted = true,
        };
    }
}

internal sealed class JsIdbOpenDbRequest(Engine engine, Realm realm)
    : JsIdbRequest(engine, realm, Null, null, open: true)
{
    internal JsIdbDatabase? Database;
}
#endif
