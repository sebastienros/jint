using System;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class PromiseTests
    {
        public Task<int> TestAsyncProperty
        {
            get { return Task.Run(() => 66); }
        }

        public async Task<int> TestAsyncMethod(int x, int y)
        {
            await Task.Delay(100);

            return x * y;
        }

        public async Task TestAsyncMethodNoReturnValue()
        {
            await Task.Delay(100);
        }

        public async Task TestAsyncMethodThrowsException()
        {
            await Task.Delay(100);
            throw new Exception("Could not connect");
        }

        #region Ctor

        [Fact]
        public void PromiseCtorWithNoResolver_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() =>
            {
                engine.Execute("new Promise();");
            });
        }

        [Fact]
        public void PromiseCtorWithInvalidResolver_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() =>
            {
                engine.Execute("new Promise({});");
            });
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

            Assert.IsType<PromiseInstance>(engine.Execute("new Promise((resolve, reject)=>{});").GetCompletionValue());
        }

        #endregion

        #region Resolve

        [Fact]
        public async Task PromiseResolveViaResolver_ReturnsCorrectValue()
        {
            var engine = new Engine();

            Assert.Equal(66, await engine.Execute("new Promise((resolve, reject)=>{resolve(66);});").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseResolveViaStatic_ReturnsCorrectValue()
        {
            var engine = new Engine();

            Assert.Equal(66, await engine.Execute("Promise.resolve(66);").GetCompletionValueAsync());
        }
        
        #endregion

        #region Reject

        [Fact]
        public async Task PromiseRejectViaResolver_ThrowsPromiseRejectedException()
        {
            var engine = new Engine();

            var ex = await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
            {
                await engine.Execute("new Promise((resolve, reject)=>{reject('Could not connect');});").GetCompletionValueAsync();
            });

            Assert.Equal("Could not connect", ex.RejectedValue.AsString());
        }

        [Fact]
        public async Task PromiseRejectViaStatic_ThrowsPromiseRejectedException()
        {
            var engine = new Engine();

            var ex = await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
            {
                await engine.Execute("Promise.reject('Could not connect');").GetCompletionValueAsync();
            });

            Assert.Equal("Could not connect", ex.RejectedValue.AsString());
        }

        #endregion

        #region Then
        
        [Fact]
        public async Task PromiseChainedThen_HandlerCalledWithCorrectValue()
        {
            var engine = new Engine();

            Assert.Equal(44, await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => 44).then(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public void PromiseThen_ReturnsNewPromiseInstance()
        {
            var engine = new Engine();

            Assert.Equal(false, engine.Execute("var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.then();  promise1 === promise2").GetCompletionValue());
        }

        [Fact]
        public async Task PromiseThen_CalledCorrectlyOnResolve()
        {
            var engine = new Engine();

            Assert.Equal(66, await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseResolveChainedWithHandler_ResolvedAsUndefined()
        {
            var engine = new Engine();

            Assert.Equal(JsValue.Undefined, await engine.Execute("Promise.resolve(33).then(() => {});").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedThenWithUndefinedCallback_PassesThroughValueCorrectly()
        {
            var engine = new Engine();

            Assert.Equal(66, await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then().then(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedThenWithCallbackReturningUndefined_PassesThroughUndefinedCorrectly()
        {
            var engine = new Engine();

            Assert.Equal(JsValue.Undefined, await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => {}).then(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedThenThrowsError_ChainedCallsCatchWithThrownError()
        {
            var engine = new Engine();

            Assert.Equal("Thrown Error", await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => { throw 'Thrown Error'; }).catch(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedThenReturnsResolvedPromise_ChainedCallsThenWithPromiseValue()
        {
            var engine = new Engine();

            Assert.Equal(55, await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.resolve(55)).then(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedThenReturnsRejectedPromise_ChainedCallsCatchWithPromiseValue()
        {
            var engine = new Engine();

            Assert.Equal("Error Message", await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.reject('Error Message')).catch(result => resolve(result)); });").GetCompletionValueAsync());
        }

        #endregion

        #region Catch

        [Fact]
        public async Task PromiseCatch_CalledCorrectlyOnReject()
        {
            var engine = new Engine();

            Assert.Equal("Could not connect", await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseThenWithCatch_CalledCorrectlyOnReject()
        {
            var engine = new Engine();

            Assert.Equal("Could not connect", await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).then(undefined, result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedWithHandler_ResolvedAsUndefined()
        {
            var engine = new Engine();

            Assert.Equal(JsValue.Undefined, await engine.Execute("Promise.reject('error').catch(() => {});").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedCatchThen_ThenCallWithUndefined()
        {
            var engine = new Engine();

            Assert.Equal(JsValue.Undefined, await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(ex => {}).then(result => resolve(result)); });").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseChainedCatchWithUndefinedHandler_CatchChainedCorrectly()
        {
            var engine = new Engine();

            Assert.Equal("Could not connect", await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch().catch(result => resolve(result)); });").GetCompletionValueAsync());
        }

        #endregion

        #region Finally

        [Fact]
        public async Task PromiseChainedFinally_HandlerCalled()
        {
            var engine = new Engine();

            Assert.Equal(16, await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).finally(() => resolve(16)); });").GetCompletionValueAsync());
        }

        [Fact]
        public void PromiseFinally_ReturnsNewPromiseInstance()
        {
            var engine = new Engine();

            Assert.Equal(false, engine.Execute("var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.finally();  promise1 === promise2").GetCompletionValue());
        }

        [Fact]
        public async Task PromiseFinally_ResolvesWithCorrectValue()
        {
            var engine = new Engine();

            Assert.Equal(2, await engine.Execute("Promise.resolve(2).finally(() => {})").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseFinallyWithNoCallback_ResolvesWithCorrectValue()
        {
            var engine = new Engine();

            Assert.Equal(2, await engine.Execute("Promise.resolve(2).finally()").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseFinallyChained_ResolvesWithCorrectValue()
        {
            var engine = new Engine();

            Assert.Equal(2, await engine.Execute("Promise.resolve(2).finally(() => 6).finally(() => 9);").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PromiseFinallyWhichThrows_ResolvesWithError()
        {
            var engine = new Engine();

            Assert.Equal("Could not connect", await engine.Execute("new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(5)}).finally(() => {throw 'Could not connect';}).catch(result => resolve(result)); });").GetCompletionValueAsync());
        }

        #endregion

        #region All

        [Fact]
        public void PromiseAllNoArg_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() => { engine.Execute("Promise.all();"); });
        }

        [Fact]
        public void PromiseAllInvalidIterator_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() => { engine.Execute("Promise.all({});"); });
        }

        [Fact]
        public async Task PromiseAllNoPromises_ResolvesCorrectly()
        {
            var engine = new Engine();

            Assert.Equal(new object[] { 1d, 2d, 3d }, (await engine.Execute("Promise.all([1,2,3]);").GetCompletionValueAsync()).ToObject());
        }

        [Fact]
        public async Task PromiseAllMixturePromisesNoPromises_ResolvesCorrectly()
        {
            var engine = new Engine();

            Assert.Equal(new object[] { 1d, 2d, 3d }, (await engine.Execute("Promise.all([1,Promise.resolve(2),3]);").GetCompletionValueAsync()).ToObject());
        }

        [Fact]
        public async Task PromiseAllMixturePromisesNoPromisesOneRejects_ResolvesCorrectly()
        {
            var engine = new Engine();

            await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
            {
                await engine.Execute("Promise.all([1,Promise.resolve(2),3, Promise.reject('Cannot connect')]);").GetCompletionValueAsync();
            });
        }

        #endregion

        #region Race

        [Fact]
        public void PromiseRaceNoArg_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() => { engine.Execute("Promise.race();"); });
        }

        [Fact]
        public void PromiseRaceInvalidIterator_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() => { engine.Execute("Promise.race({});"); });
        }

        [Fact]
        public async Task PromiseRaceNoPromises_ResolvesCorrectly()
        {
            var engine = new Engine();

            Assert.Equal(12d, (await engine.Execute("Promise.race([12,2,3]);").GetCompletionValueAsync()).ToObject());
        }

        [Fact]
        public async Task PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly()
        {
            var engine = new Engine();

            Assert.Equal(12d, (await engine.Execute("Promise.race([12,Promise.resolve(2),3]);").GetCompletionValueAsync()).ToObject());
        }

        [Fact]
        public async Task PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly2()
        {
            var engine = new Engine();

            Assert.Equal(2d, (await engine.Execute("Promise.race([Promise.resolve(2),6,3]);").GetCompletionValueAsync()).ToObject());
        }

        [Fact]
        public async Task PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly3()
        {
            var engine = new Engine();
            
            Assert.Equal(55d, (await engine.Execute("Promise.race([new Promise((resolve,reject)=>{}),Promise.resolve(55),3]);").GetCompletionValueAsync()).ToObject());
        }

        [Fact]
        public async Task PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly4()
        {
            var engine = new Engine();

            await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
            {
                await engine.Execute("Promise.race([new Promise((resolve,reject)=>{}),Promise.reject('Could not connect'),3]);").GetCompletionValueAsync();
            });
        }

        #endregion

        #region TaskToPromise

        [Fact]
        public async Task MethodTaskToPromise_CompletesCorrectly()
        {
            var engine = new Engine();

            engine.SetValue("TestObject", this);

            Assert.Equal(12d, await engine.Execute("TestObject.testAsyncMethod(3,4);").GetCompletionValueAsync());
        }

        [Fact]
        public async Task MethodTaskToPromiseWrapped_CompletesCorrectly()
        {
            var engine = new Engine();

            engine.SetValue("TestObject", this);

            Assert.Equal(12d, await engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncMethod(3,4).then(res => resolve(res)));").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PropertyTaskToPromiseWrapped_CompletesCorrectly()
        {
            var engine = new Engine();

            engine.SetValue("TestObject", this);

            Assert.Equal(66d, await engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncProperty.then(res => resolve(res)));").GetCompletionValueAsync());
        }

        [Fact]
        public async Task MethodTaskToPromiseWrappedWithoutReturnValue_ReturnsUndefined()
        {
            var engine = new Engine();

            engine.SetValue("TestObject", this);

            Assert.Equal(JsValue.Undefined, await engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncMethodNoReturnValue().then(res => resolve(res)));").GetCompletionValueAsync());
        }

        [Fact]
        public async Task PropertyTaskToPromiseThatThrowsException_ThrowsPromiseRejectedException()
        {
            var engine = new Engine();

            engine.SetValue("TestObject", this);

            var ex = await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
            {
                await engine.Execute("TestObject.testAsyncMethodThrowsException();").GetCompletionValueAsync();
            });

            Assert.Equal("Could not connect", ((Exception)ex.RejectedValue.ToObject()).Message);
        }

        [Fact]
        public async Task PropertyTaskToPromiseThatThrowsExceptionWrapped_ThrowsPromiseRejectedException()
        {
            var engine = new Engine();

            engine.SetValue("TestObject", this);

            var ex = await Assert.ThrowsAsync<PromiseRejectedException>(async () =>
            {
                await engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncMethodThrowsException().then(res => resolve(res)).catch(err => reject(err)));").GetCompletionValueAsync();
            });

            Assert.Equal("Could not connect", ((Exception) ex.RejectedValue.ToObject()).Message);
        }

        #endregion
    }
}
