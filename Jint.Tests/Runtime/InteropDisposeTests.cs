using Jint.Runtime;

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

    /// <summary>
    /// Regression test for https://github.com/sebastienros/jint/issues/2477:
    /// EvaluateAsync + IAsyncDisposable obtained via an awaited Task must complete,
    /// not deadlock. Previously hung for 10s and threw a timeout PromiseRejectedException.
    /// </summary>
    [Fact]
    public async Task ShouldAsyncDisposeAfterAwait_EvaluateAsync()
    {
        var engine = new Engine(o => o.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        var disposable = new AsyncDisposable();
        engine.SetValue("makeDisposable", new Func<Task<AsyncDisposable>>(() =>
            Task.FromResult(disposable)));

        await engine.EvaluateAsync("""
            (async () => {
                await using d = await makeDisposable();
            })()
            """);

        disposable.Disposed.Should().BeTrue();
    }

    /// <summary>
    /// Confirms the state machine truly suspends and resumes — not just that
    /// synchronously-completed ValueTasks happen to work. Uses Task.Delay so
    /// the dispose await crosses a real async boundary.
    /// </summary>
    [Fact]
    public async Task ShouldAsyncDisposeWithGenuinelyAsyncTask()
    {
        var engine = new Engine(o => o.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        var disposable = new DelayedAsyncDisposable();
        engine.SetValue("makeDisposable", new Func<Task<DelayedAsyncDisposable>>(() =>
            Task.FromResult(disposable)));

        await engine.EvaluateAsync("""
            (async () => {
                await using d = await makeDisposable();
            })()
            """);

        disposable.Disposed.Should().BeTrue();
    }

    /// <summary>
    /// Two await-using declarations in the same block dispose LIFO across two suspensions.
    /// </summary>
    [Fact]
    public async Task ShouldAsyncDisposeMultipleResourcesLifoOrder()
    {
        var engine = new Engine(o => o.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        var order = new List<string>();
        engine.SetValue("makeA", new Func<TrackedAsyncDisposable>(() => new TrackedAsyncDisposable("A", order)));
        engine.SetValue("makeB", new Func<TrackedAsyncDisposable>(() => new TrackedAsyncDisposable("B", order)));

        await engine.EvaluateAsync("""
            (async () => {
                await using a = makeA();
                await using b = makeB();
            })()
            """);

        order.Should().Equal("B", "A");
    }

    /// <summary>
    /// Nested async functions: dispose suspension must work across function boundaries.
    /// </summary>
    [Fact]
    public async Task ShouldAsyncDisposeNestedAcrossAsyncFunctions()
    {
        var engine = new Engine(o => o.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        var disposable = new AsyncDisposable();
        engine.SetValue("makeDisposable", new Func<Task<AsyncDisposable>>(() =>
            Task.FromResult(disposable)));

        await engine.EvaluateAsync("""
            async function inner() {
                await using d = await makeDisposable();
            }
            async function outer() {
                await inner();
            }
            outer();
            """);

        disposable.Disposed.Should().BeTrue();
    }

    /// <summary>
    /// Body throws AND dispose rejects: completion must be a SuppressedError per spec.
    /// </summary>
    [Fact]
    public async Task ShouldAsyncDisposeChainsSuppressedError()
    {
        var engine = new Engine(o => o.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        var disposable = new FaultingAsyncDisposable();
        engine.SetValue("makeDisposable", new Func<FaultingAsyncDisposable>(() => disposable));

        var error = await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
        {
            await engine.EvaluateAsync("""
                (async () => {
                    await using d = makeDisposable();
                    throw new Error("body error");
                })()
                """);
        });

        // The rejected value should be a SuppressedError. .error is the (later) dispose
        // failure — a .NET AggregateException wrapper since the Task was faulted, so its
        // message starts with "One or more errors occurred." and contains "dispose fault".
        // .suppressed is the (earlier) body error with the verbatim message.
        var rejected = error.RejectedValue.AsObject();
        rejected.Get("name").AsString().Should().Be("SuppressedError");
        rejected.Get("error").AsObject().Get("message").AsString().Should().Contain("dispose fault");
        rejected.Get("suppressed").AsObject().Get("message").AsString().Should().Be("body error");
    }

    /// <summary>
    /// await using at top level of an async function body — exercises
    /// JintFunctionDefinition.AsyncBlockStart's Pattern B dispose path.
    /// </summary>
    [Fact]
    public async Task ShouldAsyncDisposeAtAsyncFunctionExit()
    {
        var engine = new Engine(o => o.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        var disposable = new AsyncDisposable();
        engine.SetValue("getAsync", () => disposable);

        await engine.EvaluateAsync("""
            (async function () {
                await using d = getAsync();
            })()
            """);

        disposable.Disposed.Should().BeTrue();
    }

    /// <summary>
    /// Async function body throws — dispose must still run, and the resulting
    /// rejection must carry the original throw (no SuppressedError since dispose succeeds).
    /// </summary>
    [Fact]
    public async Task ShouldAsyncDisposeAfterUnhandledThrow()
    {
        var engine = new Engine(o => o.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        var disposable = new AsyncDisposable();
        engine.SetValue("makeDisposable", new Func<AsyncDisposable>(() => disposable));

        var error = await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
        {
            await engine.EvaluateAsync("""
                (async () => {
                    await using d = makeDisposable();
                    throw new Error("body error");
                })()
                """);
        });

        error.RejectedValue.AsObject().Get("message").AsString().Should().Be("body error");
        disposable.Disposed.Should().BeTrue();
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

    private class DelayedAsyncDisposable : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public async ValueTask DisposeAsync()
        {
            await Task.Delay(20).ConfigureAwait(false);
            Disposed = true;
        }
    }

    private class TrackedAsyncDisposable : IAsyncDisposable
    {
        private readonly string _name;
        private readonly List<string> _order;

        public TrackedAsyncDisposable(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        public ValueTask DisposeAsync()
        {
            _order.Add(_name);
            return default;
        }
    }

    private class FaultingAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return new ValueTask(Task.FromException(new InvalidOperationException("dispose fault")));
        }
    }
#endif
}
