using Jint.Native;
using System;
using System.Dynamic;
using Jint.Runtime.Interop;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class ThreadSafetyTests
    {
        private readonly Engine _engine;

        public ThreadSafetyTests()
        {
            var options = new Options();
            options.Interop.MainThreadCheck = true;
            _engine = new Engine(options);
        }

        public class SomeClass
        {

        }

        delegate int RandoDelegate(string text, SomeClass obj);

        class CallJavascriptCallback
        {
            static RandoDelegate _func;

            static SomeClass _obj;

            public static void Register(RandoDelegate func)
            {
                _func = func;
            }

            public static void Run()
            {
                if (_func != null)
                {
                    _obj = new SomeClass();
                    var result = _func("asdfklhj", _obj);
                }
            }
        }

        [Fact]
        public void EnsureExceptionInMultiThread()
        {
            var cSharpMethodCalled = false;

            _engine.SetValue("numInStringOut2",
                new Func<int, string>(number =>
                {
                    cSharpMethodCalled = true;
                    return "C# can see that you passed: " + number;
                })
            );

            _engine.SetValue("CallJavascriptCallback", typeof(CallJavascriptCallback));

            var setTimeoutCompleted = false;
            _engine.SetValue("setTimeout", new Action<Action, int>(async (action, msec) =>
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    System.Threading.Tasks.Task.Delay(msec);
                    action();
                });

                var exception = await Assert.ThrowsAsync<System.Exception>(async () =>
                {
                    await task;
                });

                Assert.Contains("This instance of the JINT Engine was initialized with a different thread", exception.Message);
                setTimeoutCompleted = true;
            }));

            _engine.Execute(@"
			    {
				    let counter = 0;
				    var callback = function(){
					    ++counter;
					    var result = numInStringOut2(42);
				    }

				    CallJavascriptCallback.Register(function(a, b){
                        if (a != 'asdfklhj'){
                            throw new Error('CallJavascriptCallback: a does not have expected value - it equals: ' + a);
                        }
					    return 52;
				    });

				    setTimeout(callback, 5);
			    }
		    ");

            try
            {
                CallJavascriptCallback.Run();
            }
            catch
            {
                // Currently this Exception is JavaScriptException "a is not defined" - but the authors of JINT may change what exception is thrown in this situation - so not testing that
            }
            for (int i = 0; (i < 100) && (!setTimeoutCompleted); ++i) // wait until setTimeout callback completes - in this case it will throw
            {
                System.Threading.Thread.Sleep(10);
            }

        }

    }
}
