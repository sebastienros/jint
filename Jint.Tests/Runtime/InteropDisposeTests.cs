namespace Jint.Tests.Runtime;

public class InteropDisposeTests
{
    private readonly Engine _engine;
    private readonly SyncDisposable _syncDisposable = new();

#if NETCOREAPP
    private readonly AsyncDisposable _asyncDisposable = new();
#endif

    public InteropDisposeTests()
    {
        _engine = new Engine();
        _engine.SetValue("getSync", () => _syncDisposable);

#if NETCOREAPP
        _engine.SetValue("getAsync", () => _asyncDisposable);
#endif
    }

    [Theory]
    [InlineData("{ using temp = getSync(); }")]
    [InlineData("(function x() { using temp = getSync(); })()")]
    [InlineData("class X { constructor() { using temp = getSync(); } } new X();")]
    [InlineData("class X { static { using temp = getSync(); } } new X();")]
    [InlineData("for (let i = 0; i < 1; i++) { using temp = getSync(); }")]
    public void ShouldSyncDispose(string program)
    {
        _engine.Execute(program);
        _syncDisposable.Disposed.Should().BeTrue();
    }

    private class SyncDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

#if NETCOREAPP
    [Theory]
    [InlineData("(async function x() { await using temp = getAsync(); })();")]
    public void ShouldAsyncDispose(string program)
    {
        _engine.Evaluate(program).UnwrapIfPromise();
        _asyncDisposable.Disposed.Should().BeTrue();
    }

    private class AsyncDisposable : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return default;
        }
    }
#endif
}
