using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

// obsolete GetCompletionValue
#pragma warning disable 618

namespace Jint.Tests.Runtime;

public class PromiseTests
{
    [Fact]
    public void RegisterPromise_CalledWithinExecute_ResolvesCorrectly()
    {
        Action<JsValue> resolveFunc = null;

        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, resolve, _) = engine.RegisterPromise();
            resolveFunc = resolve;
            return promise;
        }));

        var promise = engine.Evaluate("f();");

        resolveFunc(66);
        Assert.Equal(66, promise.UnwrapIfPromise());
    }

    [Fact]
    public void RegisterPromise_CalledWithinExecute_RejectsCorrectly()
    {
        Action<JsValue> rejectFunc = null;

        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, _, reject) = engine.RegisterPromise();
            rejectFunc = reject;
            return promise;
        }));

        engine.Execute("f();");

        var completion = engine.Evaluate("f();");

        rejectFunc("oops!");

        var ex = Assert.Throws<PromiseRejectedException>(() => { completion.UnwrapIfPromise(); });

        Assert.Equal("oops!", ex.RejectedValue.AsString());
    }

    [Fact]
    public void RegisterPromise_UsedWithRace_WorksFlawlessly()
    {
        var engine = new Engine();

        Action<JsValue> resolve1 = null;
        engine.SetValue("f1", new Func<JsValue>(() =>
        {
            var (promise, resolve, _) = engine.RegisterPromise();
            resolve1 = resolve;
            return promise;
        }));

        Action<JsValue> resolve2 = null;
        engine.SetValue("f2", new Func<JsValue>(() =>
        {
            var (promise, resolve, _) = engine.RegisterPromise();
            resolve2 = resolve;
            return promise;
        }));

        var completion = engine.Evaluate("Promise.race([f1(), f2()]);");

        resolve1("first");

        // still not finished but the promise is fulfilled
        Assert.Equal("first", completion.UnwrapIfPromise());

        resolve2("second");

        // completion value hasn't changed
        Assert.Equal("first", completion.UnwrapIfPromise());
    }

    [Fact]
    public void Execute_ConcurrentNormalExecuteCall_WorksFine()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() => engine.RegisterPromise().Promise));

        engine.Execute("f();");

        Assert.Equal(true, engine.Evaluate(" 1 + 1 === 2"));
    }

    [Fact]
    public void PromiseCtorWithNoResolver_Throws()
    {
        var engine = new Engine();

        Assert.Throws<JavaScriptException>(() => { engine.Execute("new Promise();"); });
    }

    [Fact]
    public void PromiseCtorWithInvalidResolver_Throws()
    {
        var engine = new Engine();

        Assert.Throws<JavaScriptException>(() => { engine.Execute("new Promise({});"); });
    }

    [Fact]
    public void PromiseCtorWithValidResolver_DoesNotThrow()
    {
        var engine = new Engine();

        engine.Execute("new Promise((resolve, reject)=>{});");
    }

    [Fact]
    public void PromiseCtor_ReturnsPromiseJsValue()
    {
        var engine = new Engine();
        var promise = engine.Evaluate("new Promise((resolve, reject)=>{});");

        Assert.IsType<JsPromise>(promise);
    }

    [Fact]
    public void PromiseResolveViaResolver_ReturnsCorrectValue()
    {
        var engine = new Engine();
        var res = engine.Evaluate("new Promise((resolve, reject)=>{resolve(66);});").UnwrapIfPromise();
        Assert.Equal(66, res);
    }

    [Fact]
    public void PromiseResolveViaStatic_ReturnsCorrectValue()
    {
        var engine = new Engine();
        Assert.Equal(66, engine.Evaluate("Promise.resolve(66);").UnwrapIfPromise());
    }

    [Fact]
    public void PromiseRejectViaResolver_ThrowsPromiseRejectedException()
    {
        var engine = new Engine();

        var ex = Assert.Throws<PromiseRejectedException>(() =>
        {
            engine.Evaluate("new Promise((resolve, reject)=>{reject('Could not connect');});").UnwrapIfPromise();
        });

        Assert.Equal("Could not connect", ex.RejectedValue.AsString());
    }

    [Fact]
    public void PromiseRejectViaStatic_ThrowsPromiseRejectedException()
    {
        var engine = new Engine();

        var ex = Assert.Throws<PromiseRejectedException>(() =>
        {
            engine.Evaluate("Promise.reject('Could not connect');").UnwrapIfPromise();
        });

        Assert.Equal("Could not connect", ex.RejectedValue.AsString());
    }

    [Fact]
    public void PromiseChainedThen_HandlerCalledWithCorrectValue()
    {
        var engine = new Engine();

        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => 44).then(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal(44, res);
    }

    [Fact]
    public void PromiseThen_ReturnsNewPromiseInstance()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.then();  promise1 === promise2").UnwrapIfPromise();

        Assert.Equal(false, res);
    }

    [Fact]
    public void PromiseThen_CalledCorrectlyOnResolve()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal(66, res);
    }

    [Fact]
    public void PromiseResolveChainedWithHandler_ResolvedAsUndefined()
    {
        var engine = new Engine();

        Assert.Equal(JsValue.Undefined, engine.Evaluate("Promise.resolve(33).then(() => {});").UnwrapIfPromise());
    }

    [Fact]
    public void PromiseChainedThenWithUndefinedCallback_PassesThroughValueCorrectly()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then().then(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal(66, res);
    }

    [Fact]
    public void PromiseChainedThenWithCallbackReturningUndefined_PassesThroughUndefinedCorrectly()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => {}).then(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal(JsValue.Undefined, res);
    }

    [Fact]
    public void PromiseChainedThenThrowsError_ChainedCallsCatchWithThrownError()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => { throw 'Thrown Error'; }).catch(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal("Thrown Error", res);
    }

    [Fact]
    public void PromiseChainedThenReturnsResolvedPromise_ChainedCallsThenWithPromiseValue()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.resolve(55)).then(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal(55, res);
    }

    [Fact]
    public void PromiseChainedThenReturnsRejectedPromise_ChainedCallsCatchWithPromiseValue()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.reject('Error Message')).catch(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal("Error Message", res);
    }

    [Fact]
    public void PromiseCatch_CalledCorrectlyOnReject()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal("Could not connect", res);
    }

    [Fact]
    public void PromiseThenWithCatch_CalledCorrectlyOnReject()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).then(undefined, result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal("Could not connect", res);
    }

    [Fact]
    public void PromiseChainedWithHandler_ResolvedAsUndefined()
    {
        var engine = new Engine();
        Assert.Equal(JsValue.Undefined, engine.Evaluate("Promise.reject('error').catch(() => {});").UnwrapIfPromise());
    }

    [Fact]
    public void PromiseChainedCatchThen_ThenCallWithUndefined()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(ex => {}).then(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal(JsValue.Undefined, res);
    }

    [Fact]
    public void PromiseChainedCatchWithUndefinedHandler_CatchChainedCorrectly()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch().catch(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal("Could not connect", res);
    }

    [Fact]
    public void PromiseChainedFinally_HandlerCalled()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).finally(() => resolve(16)); });").UnwrapIfPromise();

        Assert.Equal(16, res);
    }

    [Fact]
    public void PromiseFinally_ReturnsNewPromiseInstance()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.finally();  promise1 === promise2");

        Assert.Equal(false, res);
    }

    [Fact]
    public void PromiseFinally_ResolvesWithCorrectValue()
    {
        var engine = new Engine();
        Assert.Equal(2, engine.Evaluate("Promise.resolve(2).finally(() => {})").UnwrapIfPromise());
    }

    [Fact]
    public void PromiseFinallyWithNoCallback_ResolvesWithCorrectValue()
    {
        var engine = new Engine();
        Assert.Equal(2, engine.Evaluate("Promise.resolve(2).finally()").UnwrapIfPromise());
    }

    [Fact]
    public void PromiseFinallyChained_ResolvesWithCorrectValue()
    {
        var engine = new Engine();

        Assert.Equal(2, engine.Evaluate("Promise.resolve(2).finally(() => 6).finally(() => 9);").UnwrapIfPromise());
    }

    [Fact]
    public void PromiseFinallyWhichThrows_ResolvesWithError()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(5)}).finally(() => {throw 'Could not connect';}).catch(result => resolve(result)); });").UnwrapIfPromise();

        Assert.Equal("Could not connect", res);
    }

    [Fact]
    public void PromiseAll_BadIterable_Rejects()
    {
        var engine = new Engine();
        Assert.Throws<PromiseRejectedException>(() => { engine.Evaluate("Promise.all();").UnwrapIfPromise(); });
    }


    [Fact]
    public void PromiseAll_ArgsAreNotPromises_ResolvesCorrectly()
    {
        var engine = new Engine();

        Assert.Equal(new object[] {1d, 2d, 3d}, engine.Evaluate("Promise.all([1,2,3]);").UnwrapIfPromise().ToObject());
    }

    [Fact]
    public void PromiseAll_MixturePromisesNoPromises_ResolvesCorrectly()
    {
        var engine = new Engine();
        Assert.Equal(new object[] {1d, 2d, 3d},
            engine.Evaluate("Promise.all([1,Promise.resolve(2),3]);").UnwrapIfPromise().ToObject());
    }

    [Fact]
    public void PromiseAll_MixturePromisesNoPromisesOneRejects_ResolvesCorrectly()
    {
        var engine = new Engine();

        Assert.Throws<PromiseRejectedException>(() =>
        {
            engine.Evaluate("Promise.all([1,Promise.resolve(2),3, Promise.reject('Cannot connect')]);").UnwrapIfPromise();
        });
    }

    [Fact]
    public void PromiseRace_NoArgs_Rejects()
    {
        var engine = new Engine();

        Assert.Throws<PromiseRejectedException>(() => { engine.Evaluate("Promise.race();").UnwrapIfPromise(); });
    }

    [Fact]
    public void PromiseRace_InvalidIterator_Rejects()
    {
        var engine = new Engine();

        Assert.Throws<PromiseRejectedException>(() => { engine.Evaluate("Promise.race({});").UnwrapIfPromise(); });
    }

    [Fact]
    public void PromiseRaceNoPromises_ResolvesCorrectly()
    {
        var engine = new Engine();

        Assert.Equal(12d, engine.Evaluate("Promise.race([12,2,3]);").UnwrapIfPromise().ToObject());
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly()
    {
        var engine = new Engine();

        Assert.Equal(12d, engine.Evaluate("Promise.race([12,Promise.resolve(2),3]);").UnwrapIfPromise().ToObject());
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly2()
    {
        var engine = new Engine();

        Assert.Equal(2d, engine.Evaluate("Promise.race([Promise.resolve(2),6,3]);").UnwrapIfPromise().ToObject());
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly3()
    {
        var engine = new Engine();
        var res = engine.Evaluate("Promise.race([new Promise((resolve,reject)=>{}),Promise.resolve(55),3]);").UnwrapIfPromise();

        Assert.Equal(55d, res.ToObject());
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly4()
    {
        var engine = new Engine();

        Assert.Throws<PromiseRejectedException>(() =>
        {
            engine.Evaluate(
                "Promise.race([new Promise((resolve,reject)=>{}),Promise.reject('Could not connect'),3]);").UnwrapIfPromise();
        });
    }

    [Fact]
    public void PromiseRegression_SingleElementArrayWithClrDictionaryInPromiseAll()
    {
        var engine = new Engine();
        var dictionary = new Dictionary<string, object>
        {
            { "Value 1", 1 },
            { "Value 2", "a string" }
        };
        engine.SetValue("clrDictionary", dictionary);

        var resultAsObject = engine
            .Evaluate(@"
const promiseArray = [clrDictionary];
return Promise.all(promiseArray);") // Returning and array through Promise.any()
            .UnwrapIfPromise()
            .ToObject();

        var result = (object[]) resultAsObject;

        Assert.Single(result);
        Assert.IsType<Dictionary<string, object>>(result[0]);
    }

    [Fact]
    public void ManualPromise_HasCorrectStackTrace()
    {
        using var engine = new Engine();

        string logMessage = null;
        var promise = engine.RegisterPromise();
        engine.SetValue("log", new Action<JsValue>((error) => {
            logMessage = (error as ObjectInstance)["stack"].ToString();
        }));
        engine.SetValue("getPromise", new Func<JsValue>(() => promise.Promise));
        engine.Execute( "const thePromise = getPromise(); thePromise.then(() => new Error()).then(e => log(e));" );

        // Calling this method will execute the JavaScript again.
        promise.Resolve(JsValue.Undefined);

        Assert.Equal("at <anonymous>:1:56", logMessage?.Trim());
    }

    [Fact]
    public void WithResolvers_calling_resolve_resolves_promise()
    {
        // Arrange
        using var engine = new Engine();
        List<string> logMessages = [];
        engine.SetValue("log", logMessages.Add);

        // Act
        engine.Execute("""
                       const p = Promise.withResolvers();
                       const next = p.promise
                           .then(() => log('resolved'))
                           .catch(() => log('rejected'));
                           
                       log('start');
                       p.resolve();
                       log('end');
                       """);
        engine.RunAvailableContinuations();

        // Assert
        List<string> expected = ["start", "end", "resolved"];
        Assert.Equal(expected, logMessages);
    }

    [Fact]
    public void WithResolvers_calling_reject_rejects_promise()
    {
        // Arrange
        using var engine = new Engine();
        List<string> logMessages = [];
        engine.SetValue("log", logMessages.Add);

        // Act
        engine.Execute("""
                       const p = Promise.withResolvers();
                       const next = p.promise
                           .then(() => log('resolved'))
                           .catch(() => log('rejected'));

                       log('start');
                       p.reject();
                       log('end');
                       """);
        engine.RunAvailableContinuations();

        // Assert
        List<string> expected = ["start", "end", "rejected"];
        Assert.Equal(expected, logMessages);
    }

    [Fact]
    public void UnwrapIfPromise_WithCancellationToken_ResolvesCorrectly()
    {
        Action<JsValue> resolveFunc = null!;

        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, resolve, _) = engine.RegisterPromise();
            resolveFunc = resolve;
            return promise;
        }));

        var promise = engine.Evaluate("f();");

        using var cts = new CancellationTokenSource();
        resolveFunc(42);
        Assert.Equal(42, promise.UnwrapIfPromise(cts.Token));
    }

    [Fact]
    public void UnwrapIfPromise_WithCancellationToken_ThrowsOperationCanceledException()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, _, _) = engine.RegisterPromise();
            return promise;
        }));

        var promise = engine.Evaluate("f();");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Assert.Throws<OperationCanceledException>(() => promise.UnwrapIfPromise(cts.Token));
    }

    [Fact]
    public void UnwrapIfPromise_WithCancellationToken_NonPromiseReturnsValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("42");

        using var cts = new CancellationTokenSource();
        Assert.Equal(42, result.UnwrapIfPromise(cts.Token));
    }

    [Fact]
    public void UnwrapIfPromise_WithCancellationToken_RejectsCorrectly()
    {
        Action<JsValue> rejectFunc = null!;

        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, _, reject) = engine.RegisterPromise();
            rejectFunc = reject;
            return promise;
        }));

        var promise = engine.Evaluate("f();");

        using var cts = new CancellationTokenSource();
        rejectFunc("error!");

        var ex = Assert.Throws<PromiseRejectedException>(() => promise.UnwrapIfPromise(cts.Token));
        Assert.Equal("error!", ex.RejectedValue.AsString());
    }

    [Fact]
    public async Task UnwrapIfPromiseAsync_ResolvesCorrectly()
    {
        Action<JsValue> resolveFunc = null!;

        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, resolve, _) = engine.RegisterPromise();
            resolveFunc = resolve;
            return promise;
        }));

        var promise = engine.Evaluate("f();");

        resolveFunc(42);
        var result = await promise.UnwrapIfPromiseAsync();
        Assert.Equal(42, result.AsInteger());
    }

    [Fact]
    public async Task UnwrapIfPromiseAsync_RejectsCorrectly()
    {
        Action<JsValue> rejectFunc = null!;

        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, _, reject) = engine.RegisterPromise();
            rejectFunc = reject;
            return promise;
        }));

        var promise = engine.Evaluate("f();");

        rejectFunc("error!");

        var ex = await Assert.ThrowsAsync<PromiseRejectedException>(async () => await promise.UnwrapIfPromiseAsync());
        Assert.Equal("error!", ex.RejectedValue.AsString());
    }

    [Fact]
    public async Task UnwrapIfPromiseAsync_NonPromiseReturnsValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("42");

        var unwrapped = await result.UnwrapIfPromiseAsync();
        Assert.Equal(42, unwrapped.AsInteger());
    }

    [Fact]
    public async Task UnwrapIfPromiseAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() =>
        {
            var (promise, _, _) = engine.RegisterPromise();
            return promise;
        }));

        var promise = engine.Evaluate("f();");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await promise.UnwrapIfPromiseAsync(cts.Token));
    }

    [Fact]
    public async Task UnwrapIfPromiseAsync_WithIOBoundTask_DoesNotBlockCallerThread()
    {
        var engine = new Engine();
        var ioStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        engine.SetValue("simulateIO", new Func<Task<int>>(async () =>
        {
            ioStarted.TrySetResult(true);
            await Task.Delay(100);
            return 99;
        }));

        var jsPromise = engine.Evaluate("(async () => await simulateIO())()");

        // Kick off the async unwrap (should not block)
        var unwrapTask = jsPromise.UnwrapIfPromiseAsync();

        // Wait for IO to start
        await ioStarted.Task;

        // The unwrap task should still be pending while IO is in flight
        Assert.False(unwrapTask.IsCompleted, "UnwrapIfPromiseAsync should not block; task should still be pending during IO");

        var result = await unwrapTask;
        Assert.Equal(99, result.AsInteger());
    }

    // ========================================================================
    // Internal-continuation reaction allocation cut — spec-observability pins.
    // These guard the engine-internal await/reaction fast paths against
    // regressing microtask ordering, unhandled-rejection tracking, thenable
    // adoption, species subclassing, and resolving-function observability.
    // ========================================================================

    [Fact]
    public void AwaitAndThenInterleaveInSpecMicrotaskOrder()
    {
        // Classic resolved-await vs then interleaving. `await` costs one microtask tick,
        // so the two async steps interleave with the three .then steps in a fixed pattern.
        var engine = new Engine();
        engine.Evaluate("var log = [];");

        engine.Execute("""
            async function a() {
                log.push('a1');
                await Promise.resolve();
                log.push('a2');
                await Promise.resolve();
                log.push('a3');
            }
            Promise.resolve()
                .then(function () { log.push('t1'); })
                .then(function () { log.push('t2'); })
                .then(function () { log.push('t3'); });
            a();
        """);

        var log = engine.GetValue("log").AsArray();
        string[] expected = ["a1", "t1", "a2", "t2", "a3", "t3"];
        Assert.Equal(expected, log.Select(x => x.AsString()).ToArray());
    }

    [Fact]
    public void AwaitOfPrimitiveStillCostsExactlyOneTick()
    {
        // The primitive-await fast path must not skip the microtask: `await 1` interleaves
        // with a competing then-chain exactly like `await Promise.resolve(1)` would.
        var engine = new Engine();
        engine.Evaluate("var log = [];");

        engine.Execute("""
            async function a() {
                log.push('a1');
                await 1;
                log.push('a2');
                await 2;
                log.push('a3');
            }
            Promise.resolve()
                .then(function () { log.push('t1'); })
                .then(function () { log.push('t2'); })
                .then(function () { log.push('t3'); });
            a();
        """);

        var log = engine.GetValue("log").AsArray();
        string[] expected = ["a1", "t1", "a2", "t2", "a3", "t3"];
        Assert.Equal(expected, log.Select(x => x.AsString()).ToArray());
    }

    [Fact]
    public void AwaitedRejectionCaughtInsideAsyncFunctionIsTrackedThenHandled()
    {
        var engine = new Engine();
        var operations = new List<PromiseRejectionOperation>();
        engine.Advanced.PromiseRejectionTracker += (_, args) => operations.Add(args.Operation);

        engine.Evaluate("var caught = '';");
        engine.Execute("""
            (async function () {
                try {
                    await Promise.reject('boom');
                } catch (e) {
                    caught = e;
                }
            })();
        """);
        engine.Advanced.ProcessTasks();

        Assert.Equal("boom", engine.GetValue("caught").AsString());
        // Promise.reject creates an already-rejected promise (fires Reject), and the await's
        // internal continuation attaches a handler via PerformPromiseThen (fires Handle).
        // The internal-continuation path must keep firing BOTH tracker operations, exactly
        // like an explicit .catch() would — the rejection is ultimately handled.
        Assert.Equal([PromiseRejectionOperation.Reject, PromiseRejectionOperation.Handle], operations);
    }

    [Fact]
    public void UncaughtAwaitedRejectionRejectsTheAsyncFunctionsPromise()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            (async function () {
                await Promise.reject('propagated');
            })();
        """);

        var ex = Assert.Throws<PromiseRejectedException>(() => result.UnwrapIfPromise());
        Assert.Equal("propagated", ex.RejectedValue.AsString());
    }

    [Fact]
    public void AwaitAdoptsThenableViaResolve()
    {
        // Awaiting a non-promise thenable must run PromiseResolve (read .then, adopt it),
        // which the fast path preserves for object values.
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function () {
                return await { then: function (resolve) { resolve(42); } };
            })();
        """);

        Assert.Equal(42, result.UnwrapIfPromise().AsInteger());
    }

    [Fact]
    public void ThenGetterThrowIsCaughtAsRejectionOfAwait()
    {
        // The .then read during PromiseResolve can throw (a getter); that must reject,
        // and the await must observe it as a throw.
        var engine = new Engine();
        engine.Evaluate("var caught = '';");
        engine.Execute("""
            (async function () {
                try {
                    await { get then() { throw 'getter-boom'; } };
                } catch (e) {
                    caught = e;
                }
            })();
        """);
        engine.Advanced.ProcessTasks();

        Assert.Equal("getter-boom", engine.GetValue("caught").AsString());
    }

    [Fact]
    public void ThenOnSubclassUsesSpeciesConstructorForResultCapability()
    {
        // Promise.prototype.then goes through SpeciesConstructor; a subclass must NOT hit the
        // intrinsic fast path — the result capability must be an instance of the subclass.
        var engine = new Engine();
        engine.Evaluate("var subIsMy = false; var subVal = 0;");
        engine.Execute("""
            class MyPromise extends Promise {}
            var sub = MyPromise.resolve(7).then(function (x) { return x + 1; });
            subIsMy = sub instanceof MyPromise;
            sub.then(function (v) { subVal = v; });
        """);
        engine.Advanced.ProcessTasks();

        Assert.True(engine.GetValue("subIsMy").AsBoolean());
        Assert.Equal(8, engine.GetValue("subVal").AsInteger());
    }

    [Fact]
    public void ExecutorResolveFunctionsAreCallableIdempotentAndObservable()
    {
        // `new Promise(executor)` must still hand the executor real resolving functions
        // with name "" / length 1, and resolve/reject share one [[AlreadyResolved]].
        var engine = new Engine();
        var result = engine.Evaluate("""
            var meta;
            var p = new Promise(function (resolve, reject) {
                meta = typeof resolve + '/' + resolve.length + '/' + JSON.stringify(resolve.name)
                     + '/' + (typeof reject) + '/' + (resolve === reject);
                resolve(11);
                resolve(22); // idempotent - ignored
                reject(33);  // idempotent - ignored
            });
            p.then(function (v) { meta = meta + '/' + v; });
            meta;
        """);
        // meta captured before the .then microtask runs
        Assert.Equal("function/1/\"\"/function/false", result.AsString());

        var settled = engine.GetValue("p");
        Assert.Equal(11, settled.UnwrapIfPromise().AsInteger());
    }

    [Fact]
    public void WithResolversExposesCallableIdempotentResolvingFunctions()
    {
        // Promise.withResolvers goes through NewPromiseCapability on the intrinsic (fast path);
        // the escaping resolve/reject must still be real, stable, idempotent functions.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var wr = Promise.withResolvers();
            var meta = typeof wr.resolve + '/' + wr.resolve.length + '/' + JSON.stringify(wr.resolve.name)
                     + '/' + (wr.resolve === wr.resolve);
            wr.resolve('first');
            wr.resolve('second'); // idempotent
            wr.reject('nope');    // idempotent
            meta;
        """);
        Assert.Equal("function/1/\"\"/true", result.AsString());

        Assert.Equal("first", engine.GetValue("wr").AsObject().Get("promise").UnwrapIfPromise().AsString());
    }

    [Fact]
    public void PromiseAllMixesResolvedPromisesAndPlainValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.all([Promise.resolve(1), 2, Promise.resolve(3)]).then(function (r) { return r.join('-'); });
        """);

        Assert.Equal("1-2-3", result.UnwrapIfPromise().AsString());
    }

    [Fact]
    public void LongAwaitLoopAccumulatesCorrectly()
    {
        // Exercises the resolved-promise await fast path at volume (the AwaitResolvedLoop shape).
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function () {
                var s = 0;
                for (var i = 0; i < 1000; i++) { s += await Promise.resolve(1); }
                return s;
            })();
        """);

        Assert.Equal(1000, result.UnwrapIfPromise().AsInteger());
    }
}
