#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Runtime;

namespace Jint.WebApi.IndexedDb;

/// <summary>Owns engine-affine activity and posts storage notifications: https://w3c.github.io/IndexedDB/#transaction-lifetime.</summary>
internal sealed class IndexedDbAgent
{
    private readonly Engine _engine;
    private readonly HashSet<JsIdbTransaction> _transactions = [];
    private readonly HashSet<JsIdbDatabase> _connections = [];
    private readonly HashSet<IndexedDbStore.OpenOperation> _opens = [];

    internal IndexedDbAgent(Engine engine)
    {
        _engine = engine;
    }

    internal IndexedDbStore Store { get; private set; } = new();
    internal bool OpaqueOrigin { get; private set; }

    internal void Configure(IndexedDbStore store, bool opaqueOrigin)
    {
        if (_connections.Count != 0 || _transactions.Count != 0 || _opens.Count != 0)
        {
            Throw.InvalidOperationException("IndexedDB storage must be configured before opening a database.");
        }
        Store = store;
        OpaqueOrigin = opaqueOrigin;
    }

    internal Action<T> Poster<T>(Action<T> callback)
    {
        var registration = _engine.CaptureEventLoopRegistration();
        return value => _engine.AddToEventLoop(() => Run(() => callback(value)), registration, EventLoopJobKind.Task);
    }

    internal void Post(Action action)
    {
        var registration = _engine.CaptureEventLoopRegistration();
        _engine.AddToEventLoop(() => Run(action), registration, EventLoopJobKind.Task);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (JavaScriptException)
        {
            // Request dispatch handles its owning transaction. Other listener failures must not
            // close unrelated connections or abandon queued database operations.
            throw;
        }
        catch
        {
            // Never translate constraints or other non-script failures into a successful IDB request.
            Reset();
            throw;
        }
    }

    internal void Track(IndexedDbStore.OpenOperation operation) => _opens.Add(operation);
    internal void Track(JsIdbDatabase database) => _connections.Add(database);
    internal void Track(JsIdbTransaction transaction) => _transactions.Add(transaction);
    internal void Forget(JsIdbDatabase database) => _connections.Remove(database);
    internal void Forget(JsIdbTransaction transaction) => _transactions.Remove(transaction);

    internal void Complete(IndexedDbStore.OpenOperation operation)
    {
        _opens.Remove(operation);
        Store.CompleteOpen(operation);
    }

    internal void Cleanup()
    {
        foreach (var transaction in _transactions.ToArray()) transaction.Cleanup();
    }

    internal void Reset()
    {
        foreach (var transaction in _transactions.ToArray()) transaction.Abandon();
        foreach (var connection in _connections.ToArray()) connection.Close();
        foreach (var operation in _opens.ToArray())
        {
            if (operation.Connection is { } connection) Store.Close(connection);
            Store.CompleteOpen(operation);
        }
        _opens.Clear();
        _connections.Clear();
        _transactions.Clear();
    }
}
#endif
