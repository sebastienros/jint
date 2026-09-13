namespace Jint.Runtime;

/// <summary>Enters a realm for host construction or callback invocation and restores the caller on exit.</summary>
internal readonly struct RealmScope : IDisposable
{
    private readonly Engine? _engine;

    internal RealmScope(Engine engine, Realm realm)
    {
        if (!ReferenceEquals(engine.Realm, realm))
        {
            engine.EnterExecutionContext(realm.GlobalEnv, realm.GlobalEnv, realm, null, engine.Options.Strict);
            _engine = engine;
        }
    }

    public void Dispose() => _engine?.LeaveExecutionContext();
}
