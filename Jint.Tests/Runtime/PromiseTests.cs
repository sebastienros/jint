using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Array;
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
    }
}
