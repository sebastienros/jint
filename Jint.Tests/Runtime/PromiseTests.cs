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
        promise.UnwrapIfPromise().Should().Be(66);
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

        var ex = Invoking(() => { completion.UnwrapIfPromise(); }).Should().ThrowExactly<PromiseRejectedException>().Which;

        ex.RejectedValue.AsString().Should().Be("oops!");
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
        completion.UnwrapIfPromise().Should().Be("first");

        resolve2("second");

        // completion value hasn't changed
        completion.UnwrapIfPromise().Should().Be("first");
    }

    [Fact]
    public void Execute_ConcurrentNormalExecuteCall_WorksFine()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<JsValue>(() => engine.RegisterPromise().Promise));

        engine.Execute("f();");

        engine.Evaluate(" 1 + 1 === 2").Should().BeTrue();
    }

    [Fact]
    public void PromiseCtorWithNoResolver_Throws()
    {
        var engine = new Engine();

        Invoking(() => { engine.Execute("new Promise();"); }).Should().ThrowExactly<JavaScriptException>();
    }

    [Fact]
    public void PromiseCtorWithInvalidResolver_Throws()
    {
        var engine = new Engine();

        Invoking(() => { engine.Execute("new Promise({});"); }).Should().ThrowExactly<JavaScriptException>();
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

        promise.Should().BeOfType<JsPromise>();
    }

    [Fact]
    public void PromiseResolveViaResolver_ReturnsCorrectValue()
    {
        var engine = new Engine();
        var res = engine.Evaluate("new Promise((resolve, reject)=>{resolve(66);});").UnwrapIfPromise();
        res.Should().Be(66);
    }

    [Fact]
    public void PromiseResolveViaStatic_ReturnsCorrectValue()
    {
        var engine = new Engine();
        engine.Evaluate("Promise.resolve(66);").UnwrapIfPromise().Should().Be(66);
    }

    [Fact]
    public void PromiseRejectViaResolver_ThrowsPromiseRejectedException()
    {
        var engine = new Engine();

        var ex = Invoking(() =>
        {
            engine.Evaluate("new Promise((resolve, reject)=>{reject('Could not connect');});").UnwrapIfPromise();
        }).Should().ThrowExactly<PromiseRejectedException>().Which;

        ex.RejectedValue.AsString().Should().Be("Could not connect");
    }

    [Fact]
    public void PromiseRejectViaStatic_ThrowsPromiseRejectedException()
    {
        var engine = new Engine();

        var ex = Invoking(() =>
        {
            engine.Evaluate("Promise.reject('Could not connect');").UnwrapIfPromise();
        }).Should().ThrowExactly<PromiseRejectedException>().Which;

        ex.RejectedValue.AsString().Should().Be("Could not connect");
    }

    [Fact]
    public void PromiseChainedThen_HandlerCalledWithCorrectValue()
    {
        var engine = new Engine();

        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => 44).then(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be(44);
    }

    [Fact]
    public void PromiseThen_ReturnsNewPromiseInstance()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.then();  promise1 === promise2").UnwrapIfPromise();

        res.Should().BeFalse();
    }

    [Fact]
    public void PromiseThen_CalledCorrectlyOnResolve()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be(66);
    }

    [Fact]
    public void PromiseResolveChainedWithHandler_ResolvedAsUndefined()
    {
        var engine = new Engine();

        engine.Evaluate("Promise.resolve(33).then(() => {});").UnwrapIfPromise().Should().BeUndefined();
    }

    [Fact]
    public void PromiseChainedThenWithUndefinedCallback_PassesThroughValueCorrectly()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then().then(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be(66);
    }

    [Fact]
    public void PromiseChainedThenWithCallbackReturningUndefined_PassesThroughUndefinedCorrectly()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => {}).then(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().BeUndefined();
    }

    [Fact]
    public void PromiseChainedThenThrowsError_ChainedCallsCatchWithThrownError()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => { throw 'Thrown Error'; }).catch(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be("Thrown Error");
    }

    [Fact]
    public void PromiseChainedThenReturnsResolvedPromise_ChainedCallsThenWithPromiseValue()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.resolve(55)).then(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be(55);
    }

    [Fact]
    public void PromiseChainedThenReturnsRejectedPromise_ChainedCallsCatchWithPromiseValue()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.reject('Error Message')).catch(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be("Error Message");
    }

    [Fact]
    public void PromiseCatch_CalledCorrectlyOnReject()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be("Could not connect");
    }

    [Fact]
    public void PromiseThenWithCatch_CalledCorrectlyOnReject()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).then(undefined, result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be("Could not connect");
    }

    [Fact]
    public void PromiseChainedWithHandler_ResolvedAsUndefined()
    {
        var engine = new Engine();
        engine.Evaluate("Promise.reject('error').catch(() => {});").UnwrapIfPromise().Should().BeUndefined();
    }

    [Fact]
    public void PromiseChainedCatchThen_ThenCallWithUndefined()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(ex => {}).then(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().BeUndefined();
    }

    [Fact]
    public void PromiseChainedCatchWithUndefinedHandler_CatchChainedCorrectly()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch().catch(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be("Could not connect");
    }

    [Fact]
    public void PromiseChainedFinally_HandlerCalled()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).finally(() => resolve(16)); });").UnwrapIfPromise();

        res.Should().Be(16);
    }

    [Fact]
    public void PromiseFinally_ReturnsNewPromiseInstance()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.finally();  promise1 === promise2");

        res.Should().BeFalse();
    }

    [Fact]
    public void PromiseFinally_ResolvesWithCorrectValue()
    {
        var engine = new Engine();
        engine.Evaluate("Promise.resolve(2).finally(() => {})").UnwrapIfPromise().Should().Be(2);
    }

    [Fact]
    public void PromiseFinallyWithNoCallback_ResolvesWithCorrectValue()
    {
        var engine = new Engine();
        engine.Evaluate("Promise.resolve(2).finally()").UnwrapIfPromise().Should().Be(2);
    }

    [Fact]
    public void PromiseFinallyChained_ResolvesWithCorrectValue()
    {
        var engine = new Engine();

        engine.Evaluate("Promise.resolve(2).finally(() => 6).finally(() => 9);").UnwrapIfPromise().Should().Be(2);
    }

    [Fact]
    public void PromiseFinallyWhichThrows_ResolvesWithError()
    {
        var engine = new Engine();
        var res = engine.Evaluate(
            "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(5)}).finally(() => {throw 'Could not connect';}).catch(result => resolve(result)); });").UnwrapIfPromise();

        res.Should().Be("Could not connect");
    }

    [Fact]
    public void PromiseAll_BadIterable_Rejects()
    {
        var engine = new Engine();
        Invoking(() => { engine.Evaluate("Promise.all();").UnwrapIfPromise(); }).Should().ThrowExactly<PromiseRejectedException>();
    }


    [Fact]
    public void PromiseAll_ArgsAreNotPromises_ResolvesCorrectly()
    {
        var engine = new Engine();

        engine.Evaluate("Promise.all([1,2,3]);").UnwrapIfPromise().ToObject().Should()
            .BeEquivalentTo(new object[] { 1d, 2d, 3d }, static options => options.WithStrictOrdering());
    }

    [Fact]
    public void PromiseAll_MixturePromisesNoPromises_ResolvesCorrectly()
    {
        var engine = new Engine();
        engine.Evaluate("Promise.all([1,Promise.resolve(2),3]);").UnwrapIfPromise().ToObject().Should()
            .BeEquivalentTo(new object[] { 1d, 2d, 3d }, static options => options.WithStrictOrdering());
    }

    [Fact]
    public void PromiseAll_MixturePromisesNoPromisesOneRejects_ResolvesCorrectly()
    {
        var engine = new Engine();

        Invoking(() =>
        {
            engine.Evaluate("Promise.all([1,Promise.resolve(2),3, Promise.reject('Cannot connect')]);").UnwrapIfPromise();
        }).Should().ThrowExactly<PromiseRejectedException>();
    }

    [Fact]
    public void PromiseRace_NoArgs_Rejects()
    {
        var engine = new Engine();

        Invoking(() => { engine.Evaluate("Promise.race();").UnwrapIfPromise(); }).Should().ThrowExactly<PromiseRejectedException>();
    }

    [Fact]
    public void PromiseRace_InvalidIterator_Rejects()
    {
        var engine = new Engine();

        Invoking(() => { engine.Evaluate("Promise.race({});").UnwrapIfPromise(); }).Should().ThrowExactly<PromiseRejectedException>();
    }

    [Fact]
    public void PromiseRaceNoPromises_ResolvesCorrectly()
    {
        var engine = new Engine();

        engine.Evaluate("Promise.race([12,2,3]);").UnwrapIfPromise().ToObject().Should().Be(12d);
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly()
    {
        var engine = new Engine();

        engine.Evaluate("Promise.race([12,Promise.resolve(2),3]);").UnwrapIfPromise().ToObject().Should().Be(12d);
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly2()
    {
        var engine = new Engine();

        engine.Evaluate("Promise.race([Promise.resolve(2),6,3]);").UnwrapIfPromise().ToObject().Should().Be(2d);
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly3()
    {
        var engine = new Engine();
        var res = engine.Evaluate("Promise.race([new Promise((resolve,reject)=>{}),Promise.resolve(55),3]);").UnwrapIfPromise();

        res.ToObject().Should().Be(55d);
    }

    [Fact]
    public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly4()
    {
        var engine = new Engine();

        Invoking(() =>
        {
            engine.Evaluate(
                "Promise.race([new Promise((resolve,reject)=>{}),Promise.reject('Could not connect'),3]);").UnwrapIfPromise();
        }).Should().ThrowExactly<PromiseRejectedException>();
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

        result.Should().ContainSingle();
        result[0].Should().BeOfType<Dictionary<string, object>>();
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

        (logMessage?.Trim()).Should().Be("at <anonymous>:1:56");
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
        logMessages.Should().Equal(expected);
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
        logMessages.Should().Equal(expected);
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
        promise.UnwrapIfPromise(cts.Token).Should().Be(42);
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

        Invoking(() => promise.UnwrapIfPromise(cts.Token)).Should().ThrowExactly<OperationCanceledException>();
    }

    [Fact]
    public void UnwrapIfPromise_WithCancellationToken_NonPromiseReturnsValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("42");

        using var cts = new CancellationTokenSource();
        result.UnwrapIfPromise(cts.Token).Should().Be(42);
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

        var ex = Invoking(() => promise.UnwrapIfPromise(cts.Token)).Should().ThrowExactly<PromiseRejectedException>().Which;
        ex.RejectedValue.AsString().Should().Be("error!");
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
        result.AsInteger().Should().Be(42);
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

        var ex = (await Awaiting(async () => await promise.UnwrapIfPromiseAsync()).Should().ThrowExactlyAsync<PromiseRejectedException>()).Which;
        ex.RejectedValue.AsString().Should().Be("error!");
    }

    [Fact]
    public async Task UnwrapIfPromiseAsync_NonPromiseReturnsValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("42");

        var unwrapped = await result.UnwrapIfPromiseAsync();
        unwrapped.AsInteger().Should().Be(42);
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

        await Awaiting(async () => await promise.UnwrapIfPromiseAsync(cts.Token)).Should().ThrowAsync<OperationCanceledException>();
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
        unwrapTask.IsCompleted.Should().BeFalse("UnwrapIfPromiseAsync should not block; task should still be pending during IO");

        var result = await unwrapTask;
        result.AsInteger().Should().Be(99);
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
        log.Select(x => x.AsString()).ToArray().Should().Equal(expected);
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
        log.Select(x => x.AsString()).ToArray().Should().Equal(expected);
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

        engine.GetValue("caught").AsString().Should().Be("boom");
        // Promise.reject creates an already-rejected promise (fires Reject), and the await's
        // internal continuation attaches a handler via PerformPromiseThen (fires Handle).
        // The internal-continuation path must keep firing BOTH tracker operations, exactly
        // like an explicit .catch() would — the rejection is ultimately handled.
        operations.Should().Equal([PromiseRejectionOperation.Reject, PromiseRejectionOperation.Handle]);
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

        var ex = Invoking(() => result.UnwrapIfPromise()).Should().ThrowExactly<PromiseRejectedException>().Which;
        ex.RejectedValue.AsString().Should().Be("propagated");
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

        result.UnwrapIfPromise().AsInteger().Should().Be(42);
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

        engine.GetValue("caught").AsString().Should().Be("getter-boom");
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

        engine.GetValue("subIsMy").AsBoolean().Should().BeTrue();
        engine.GetValue("subVal").AsInteger().Should().Be(8);
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
        result.AsString().Should().Be("function/1/\"\"/function/false");

        var settled = engine.GetValue("p");
        settled.UnwrapIfPromise().AsInteger().Should().Be(11);
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
        result.AsString().Should().Be("function/1/\"\"/true");

        engine.GetValue("wr").AsObject().Get("promise").UnwrapIfPromise().AsString().Should().Be("first");
    }

    [Fact]
    public void PromiseAllMixesResolvedPromisesAndPlainValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.all([Promise.resolve(1), 2, Promise.resolve(3)]).then(function (r) { return r.join('-'); });
        """);

        result.UnwrapIfPromise().AsString().Should().Be("1-2-3");
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

        result.UnwrapIfPromise().AsInteger().Should().Be(1000);
    }
}
