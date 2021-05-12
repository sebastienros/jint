using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class PromiseTests
    {
        #region Ctor

        [Fact(Timeout = 5000)]
        public void PromiseCtorWithNoResolver_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() => { engine.Execute("new Promise();"); });
        }

        [Fact(Timeout = 5000)]
        public void PromiseCtorWithInvalidResolver_Throws()
        {
            var engine = new Engine();

            Assert.Throws<JavaScriptException>(() => { engine.Execute("new Promise({});"); });
        }

        [Fact(Timeout = 5000)]
        public void PromiseCtorWithValidResolver_DoesNotThrow()
        {
            var engine = new Engine();

            engine.Execute("new Promise((resolve, reject)=>{});");
        }

        [Fact(Timeout = 5000)]
        public void PromiseCtor_ReturnsPromiseJsValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("new Promise((resolve, reject)=>{});");
        
            Assert.IsType<PromiseInstance>(engine.GetCompletionValue());
        }

        #endregion

        #region Resolve

        [Fact(Timeout = 5000)]
        public void PromiseResolveViaResolver_ReturnsCorrectValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("new Promise((resolve, reject)=>{resolve(66);});");
            Assert.Equal(66, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseResolveViaStatic_ReturnsCorrectValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.resolve(66);");

            Assert.Equal(66, engine.GetPromiseCompletionValue());
        }

        #endregion

        #region Reject

        [Fact(Timeout = 5000)]
        public void PromiseRejectViaResolver_ThrowsPromiseRejectedException()
        {
            var engine = new Engine();
                engine.ExecuteWithEventLoop("new Promise((resolve, reject)=>{reject('Could not connect');});");

            var ex = Assert.Throws<PromiseRejectedException>(() => { engine.GetPromiseCompletionValue(); });

            Assert.Equal("Could not connect", ex.RejectedValue.AsString());
        }

        [Fact(Timeout = 5000)]
        public void PromiseRejectViaStatic_ThrowsPromiseRejectedException()
        {
            var engine = new Engine();
                engine.ExecuteWithEventLoop("Promise.reject('Could not connect');");

            var ex = Assert.Throws<PromiseRejectedException>(() => { engine.GetPromiseCompletionValue(); });

            Assert.Equal("Could not connect", ex.RejectedValue.AsString());
        }

        #endregion

        #region Then

        [Fact(Timeout = 5000)]
        public void PromiseChainedThen_HandlerCalledWithCorrectValue()
        {
            var engine = new Engine();

            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => 44).then(result => resolve(result)); });");

            Assert.Equal(44, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseThen_ReturnsNewPromiseInstance()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.then();  promise1 === promise2");

            Assert.Equal(false, engine.GetPromiseCompletionValue()
            );
        }

        [Fact(Timeout = 5000)]
        public void PromiseThen_CalledCorrectlyOnResolve()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(result => resolve(result)); });");

            Assert.Equal(66, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseResolveChainedWithHandler_ResolvedAsUndefined()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.resolve(33).then(() => {});");

            Assert.Equal(JsValue.Undefined, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedThenWithUndefinedCallback_PassesThroughValueCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then().then(result => resolve(result)); });");

            Assert.Equal(66, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedThenWithCallbackReturningUndefined_PassesThroughUndefinedCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => {}).then(result => resolve(result)); });");

            Assert.Equal(JsValue.Undefined, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedThenThrowsError_ChainedCallsCatchWithThrownError()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => { throw 'Thrown Error'; }).catch(result => resolve(result)); });");

            Assert.Equal("Thrown Error", engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedThenReturnsResolvedPromise_ChainedCallsThenWithPromiseValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.resolve(55)).then(result => resolve(result)); });");

            Assert.Equal(55, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedThenReturnsRejectedPromise_ChainedCallsCatchWithPromiseValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).then(() => Promise.reject('Error Message')).catch(result => resolve(result)); });");

            Assert.Equal("Error Message", engine.GetPromiseCompletionValue());
        }

        #endregion

        #region Catch

        [Fact(Timeout = 5000)]
        public void PromiseCatch_CalledCorrectlyOnReject()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(result => resolve(result)); });");

            Assert.Equal("Could not connect", engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseThenWithCatch_CalledCorrectlyOnReject()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).then(undefined, result => resolve(result)); });");

            Assert.Equal("Could not connect", engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedWithHandler_ResolvedAsUndefined()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.reject('error').catch(() => {});");

            Assert.Equal(JsValue.Undefined, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedCatchThen_ThenCallWithUndefined()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch(ex => {}).then(result => resolve(result)); });");

            Assert.Equal(JsValue.Undefined, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseChainedCatchWithUndefinedHandler_CatchChainedCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerReject('Could not connect')}).catch().catch(result => resolve(result)); });");

            Assert.Equal("Could not connect", engine.GetPromiseCompletionValue());
        }

        #endregion

        #region Finally

        [Fact(Timeout = 5000)]
        public void PromiseChainedFinally_HandlerCalled()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(66)}).finally(() => resolve(16)); });");

            Assert.Equal(16, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseFinally_ReturnsNewPromiseInstance()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "var promise1 = new Promise((resolve, reject) => { resolve(1); }); var promise2 = promise1.finally();  promise1 === promise2");

            Assert.Equal(false, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseFinally_ResolvesWithCorrectValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.resolve(2).finally(() => {})");

            Assert.Equal(2, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseFinallyWithNoCallback_ResolvesWithCorrectValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.resolve(2).finally()");

            Assert.Equal(2, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseFinallyChained_ResolvesWithCorrectValue()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.resolve(2).finally(() => 6).finally(() => 9);");

            Assert.Equal(2, engine.GetPromiseCompletionValue());
        }

        [Fact(Timeout = 5000)]
        public void PromiseFinallyWhichThrows_ResolvesWithError()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "new Promise((resolve, reject) => { new Promise((innerResolve, innerReject) => {innerResolve(5)}).finally(() => {throw 'Could not connect';}).catch(result => resolve(result)); });");

            Assert.Equal("Could not connect", engine.GetPromiseCompletionValue());
        }

        #endregion

        #region All

        [Fact(Timeout = 5000)]
        public void PromiseAll_BadIterable_Rejects()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.all();");
            Assert.Throws<PromiseRejectedException>(() => { engine.GetPromiseCompletionValue(); });
        }


        [Fact(Timeout = 5000)]
        public void PromiseAll_ArgsAreNotPromises_ResolvesCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.all([1,2,3]);");

            Assert.Equal(new object[] {1d, 2d, 3d}, engine.GetPromiseCompletionValue().ToObject());
        }

        [Fact(Timeout = 5000)]
        public void PromiseAll_MixturePromisesNoPromises_ResolvesCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.all([1,Promise.resolve(2),3]);");

            Assert.Equal(new object[] {1d, 2d, 3d}, engine.GetPromiseCompletionValue().ToObject());
        }

        [Fact(Timeout = 5000)]
        public void PromiseAll_MixturePromisesNoPromisesOneRejects_ResolvesCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.all([1,Promise.resolve(2),3, Promise.reject('Cannot connect')]);");

            Assert.Throws<PromiseRejectedException>(() => { engine.GetPromiseCompletionValue(); });
        }

        #endregion

        #region Race

        [Fact(Timeout = 5000)]
        public void PromiseRace_NoArgs_Rejects()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.race();");

            Assert.Throws<PromiseRejectedException>(() => { engine.GetPromiseCompletionValue(); });
        }

        [Fact(Timeout = 5000)]
        public void PromiseRace_InvalidIterator_Rejects()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.race({});");

            Assert.Throws<PromiseRejectedException>(() => { engine.GetPromiseCompletionValue(); });
        }

        [Fact(Timeout = 5000)]
        public void PromiseRaceNoPromises_ResolvesCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.race([12,2,3]);");

            Assert.Equal(12d, engine.GetPromiseCompletionValue().ToObject());
        }

        [Fact(Timeout = 5000)]
        public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.race([12,Promise.resolve(2),3]);");

            Assert.Equal(12d, engine.GetPromiseCompletionValue().ToObject());
        }

        [Fact(Timeout = 5000)]
        public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly2()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.race([Promise.resolve(2),6,3]);");

            Assert.Equal(2d, engine.GetPromiseCompletionValue().ToObject());
        }

        [Fact(Timeout = 5000)]
        public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly3()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop("Promise.race([new Promise((resolve,reject)=>{}),Promise.resolve(55),3]);");

            Assert.Equal(55d, engine.GetPromiseCompletionValue().ToObject());
        }

        [Fact(Timeout = 5000)]
        public void PromiseRaceMixturePromisesNoPromises_ResolvesCorrectly4()
        {
            var engine = new Engine();
            engine.ExecuteWithEventLoop(
                "Promise.race([new Promise((resolve,reject)=>{}),Promise.reject('Could not connect'),3]);");

            Assert.Throws<PromiseRejectedException>(() => { engine.GetPromiseCompletionValue(); });
        }

        #endregion

        #region TaskToPromise

        // [Fact(Timeout = 5000)]
        // public void MethodTaskToPromise_CompletesCorrectly()
        // {
        //     var engine = new Engine();
        //
        //     engine.SetValue("TestObject", new PromiseTestClass());
        //
        //     Assert.Equal(12d,  engine.Execute("TestObject.testAsyncMethod(3,4);"));
        // }

        // [Fact(Timeout = 5000)]
        // public void MethodTaskToPromiseWrapped_CompletesCorrectly()
        // {
        //     var engine = new Engine();
        //
        //     engine.SetValue("TestObject", new PromiseTestClass());
        //
        //     Assert.Equal(12d,  engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncMethod(3,4).then(res => resolve(res)));"));
        // }

        // [Fact(Timeout = 5000)]
        // public void PropertyTaskToPromiseWrapped_CompletesCorrectly()
        // {
        //     var engine = new Engine();
        //
        //     engine.SetValue("TestObject", new PromiseTestClass());
        //
        //     Assert.Equal(66d,  engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncProperty.then(res => resolve(res)));"));
        // }

        // [Fact(Timeout = 5000)]
        // public void MethodTaskToPromiseWrappedWithoutReturnValue_ReturnsUndefined()
        // {
        //     var engine = new Engine();
        //
        //     engine.SetValue("TestObject", new PromiseTestClass());
        //
        //     Assert.Equal(JsValue.Undefined,  engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncMethodNoReturnValue().then(res => resolve(res)));"));
        // }
        //
        // [Fact(Timeout = 5000)]
        // public void PropertyTaskToPromiseThatThrowsException_ThrowsPromiseRejectedException()
        // {
        //     var engine = new Engine();
        //
        //     engine.SetValue("TestObject", new PromiseTestClass());
        //
        //     var ex = await Assert.Throws<PromiseRejectedException>( () =>
        //     {
        //          engine.Execute("TestObject.testAsyncMethodThrowsException();");
        //     });
        //
        //     Assert.Equal("Could not connect", ((Exception)ex.RejectedValue.ToObject()).Message);
        // }
        //
        // [Fact(Timeout = 5000)]
        // public void PropertyTaskToPromiseThatThrowsExceptionWrapped_ThrowsPromiseRejectedException()
        // {
        //     var engine = new Engine();
        //
        //     engine.SetValue("TestObject", new PromiseTestClass());
        //
        //     var ex = await Assert.Throws<PromiseRejectedException>( () =>
        //     {
        //          engine.Execute("new Promise((resolve, reject) =>  TestObject.testAsyncMethodThrowsException().then(res => resolve(res)).catch(err => reject(err)));");
        //     });
        //
        //     Assert.Equal("Could not connect", ((Exception) ex.RejectedValue.ToObject()).Message);
        // }

        #endregion
    }
}