using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>Scopes construction to its owning realm, including lazy shape functions.</summary>
internal readonly struct BrowserRealmScope : IDisposable
{
    private readonly Engine? _engine;

    internal BrowserRealmScope(Engine engine, Realm realm)
    {
        if (!ReferenceEquals(engine.Realm, realm))
        {
            engine.EnterExecutionContext(realm.GlobalEnv, realm.GlobalEnv, realm, null, engine.Options.Strict);
            _engine = engine;
        }
    }

    public void Dispose() => _engine?.LeaveExecutionContext();
}
