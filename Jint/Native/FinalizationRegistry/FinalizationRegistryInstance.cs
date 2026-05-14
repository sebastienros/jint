using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.FinalizationRegistry;

internal sealed record Cell(JsValue WeakRefTarget, JsValue HeldValue, JsValue? UnregisterToken);

internal sealed class FinalizationRegistryInstance : ObjectInstance
{
    private readonly Realm _realm;
    private readonly JobCallback _callable;
    private readonly ConditionalWeakTable<JsValue, List<Observer>> _cells = new();
    private readonly Dictionary<JsValue, List<Observer>> _byToken = new();

    public FinalizationRegistryInstance(Engine engine, Realm realm, ICallable cleanupCallback) : base(engine)
    {
        _realm = realm;
        _callable = engine._host.MakeJobCallBack(cleanupCallback);
    }

    public static void CleanupFinalizationRegistry(ICallable? callback)
    {
    }

    public void AddCell(Cell cell)
    {
        var observer = new Observer(_callable);
        var observerList = _cells.GetOrCreateValue(cell.WeakRefTarget);
        observerList.Add(observer);

        if (cell.UnregisterToken is not null)
        {
            if (!_byToken.TryGetValue(cell.UnregisterToken, out var list))
            {
                _byToken[cell.UnregisterToken] = list = new List<Observer>();
            }
            list.Add(observer);
        }
    }

    public JsValue Remove(JsValue unregisterToken)
    {
        if (_byToken.TryGetValue(unregisterToken, out var list))
        {
            var any = list.Count > 0;
            list.Clear();
            return any;
        }

        return false;
    }

    private sealed class Observer
    {
        private readonly JobCallback _callable;

        public Observer(JobCallback callable)
        {
            _callable = callable;
        }

#pragma warning disable MA0055
        ~Observer()
#pragma warning restore MA0055
        {
            try
            {
                _callable.Callback.Call(Undefined);
            }
            catch
            {
                // FinalizationRegistry cleanup callbacks are spec'd to run in a Job
                // isolated from any calling context. Exceptions must never escape
                // the GC finalizer thread or they will terminate the host process.
            }
        }
    }
}
