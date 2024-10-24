using Jint.Native;
using Jint.Native.Object;
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
        List<string> logMessages = new();
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
        List<string> expected = new() { "start", "end", "resolved" };
        Assert.Equal(expected, logMessages);
    }

    [Fact]
    public void WithResolvers_calling_reject_rejects_promise()
    {
        // Arrange
        using var engine = new Engine();
        List<string> logMessages = new();
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
        List<string> expected = new() { "start", "end", "rejected" };
        Assert.Equal(expected, logMessages);
    }
}
