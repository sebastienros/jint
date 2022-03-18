using Jint.Native;
using System;
using System.Dynamic;
using Jint.Runtime.Interop;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class AsyncAndDisposeTests
    {
        private readonly Engine _engine;

        public AsyncAndDisposeTests()
        {
            _engine = new Engine(cfg => cfg
                .AllowOperatorOverloading())
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("throws", new Func<Action, Exception>(Assert.Throws<Exception>))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("assertFalse", new Action<bool>(Assert.False))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                .SetValue("CallJavascriptCallback", typeof(CallJavascriptCallback))
            ;
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
        public void NoUndefinedParameterValues()
        {
            var cSharpMethodCalled = false;
            var logString = "";

            _engine.SetValue("numInStringOut2",
                new Func<int, string>(number =>
                {
                    cSharpMethodCalled = true;
                    logString += "numInStringOut2: number: " + number;
                    return "C# can see that you passed: " + number;
                })
            );

            _engine.SetValue("CallJavascriptCallback", typeof(CallJavascriptCallback));

            _engine.SetValue("setTimeout", new Action<Action, int>((action, msec) =>
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    System.Threading.Tasks.Task.Delay(msec);
                    action();
                });
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

            CallJavascriptCallback.Run();

            for (int i = 0; (i < 100) && (!cSharpMethodCalled); ++i) // Give our setTimeout enough time to run - so that it can call the C# that will set cSharpMethodCalled to true
            {
                System.Threading.Thread.Sleep(10);
            }

            Assert.Equal(true, cSharpMethodCalled);
        }

    }
}
