using Jint.Native;
using TypeReference = Jint.Runtime.Interop.TypeReference;

namespace Jint.Tests.Runtime;

public class InteropDisposeTests
{
    private readonly Engine _engine;
    private readonly SyncDisposable _syncDisposable = new();

    public InteropDisposeTests()
    {
        _engine = new Engine();
        _engine.SetValue("getSync", (JsValue _, JsValue[] _) => _syncDisposable);
        _engine.SetValue(nameof(AsyncDisposable), TypeReference.CreateTypeReference<AsyncDisposable>(_engine));
    }

    [Fact]
    public void ShouldSyncDisposeInsideFunction()
    {
        _engine.Execute("""
                        (function x()
                        {
                            using temp = getSync();
                        })();
                        """);

        _syncDisposable.Disposed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSyncDisposeInsideClassConstructor()
    {
        _engine.Execute("""
                        class X
                        {
                            constructor() { using temp = getSync(); }
                        }
                        new X();
                        """);

        _syncDisposable.Disposed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSyncDisposeInsideClassStatic()
    {
        _engine.Execute("""
                        class X
                        {
                            static { using temp = getSync(); }
                        }
                        new X();
                        """);

        _syncDisposable.Disposed.Should().BeTrue();
    }


    [Fact]
    public void ShouldSyncDisposeInsideBlock()
    {
        _engine.Execute("""
                        {
                            using temp = getSync();
                        }
                        """);

        _syncDisposable.Disposed.Should().BeTrue();
    }


    [Fact]
    public void ShouldSyncDisposeInsideFor()
    {
        _engine.Execute("""
                        for (let i = 0; i < 1; i++)
                        {
                            using temp = getSync();
                        }
                        """);

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

    private class AsyncDisposable : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return default;
        }
    }
}
