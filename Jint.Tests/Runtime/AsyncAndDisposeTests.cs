using Jint.Native;
using System;
using System.Dynamic;
using Jint.Runtime.Interop;
using Xunit;

using Xunit.Abstractions;
using System.IO;
using System.Text;

namespace Jint.Tests.Runtime
{
    public class AsyncAndDisposeTests : IDisposable
    {
        private readonly Engine _engine;

        public AsyncAndDisposeTests(ITestOutputHelper output)
        {
            // TPC: remove the following 2 statements - just for enabling Console.WriteLine(..)
            var converter = new Converter(output);
            System.Console.SetOut(converter);


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


        /*public AsyncAndDisposeTests()
        {
        }
        */
        void IDisposable.Dispose()
        {
        }

        public class SomeClass
        {

        }

        delegate int RandoDelegate(string text, SomeClass obj);

        class CallJavascriptCallback
        {
            static RandoDelegate _func;

            static SomeClass _obj;

            //public void Register(Func<string, SomeClass, int> func)
            public static void Register(RandoDelegate func)
            {
                Console.WriteLine("Register: " + func);
                _func = func;
            }

            public static void Run()
            {
                if (_func != null)
                {
                    _obj = new SomeClass();
                    var result = _func("asdfklhj", _obj);
                    Console.WriteLine("result: " + result);
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
                    Console.WriteLine("numInStringOut2: number: " + number);
                    logString += "numInStringOut2: number: " + number;
                    //Assert.AreEqual(magicNum, number);
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
					    log('js callback: counter: ' + counter);
					    var result = numInStringOut2(42);
					    log('js callback: result: ' + result);
				    }

				    CallJavascriptCallback.Register(function(a, b){
					    log('RegisterWithMeBro.Register: a: ' + a + ' b: ' + b);
					    return 52;
				    });

				    log('before setTimeout');
				    setTimeout(callback, 5);
				    //callback();
				    log('after setTimeout');
			    }
		    ");

            CallJavascriptCallback.Run();
             
            for (int i = 0; (i < 100) && (!cSharpMethodCalled); ++i) // Give our setTimeout enough time to run - so that it can call the C# that will set cSharpMethodCalled to true
            {
                //System.GC.Collect();
                System.Threading.Thread.Sleep(10);
            }

            //CallJavascriptCallback.Run();

            //Console.WriteLine("JavascriptCallbackAfterDelay: _asyncException: " + _asyncException);

            Console.WriteLine("JavascriptCallbackAfterDelay: after Thread.Sleep: logString: " + logString);
            Assert.Equal(true, cSharpMethodCalled);
        }

        private class Converter : TextWriter
        {
            ITestOutputHelper _output;

            public Converter(ITestOutputHelper output)
            {
                _output = output;
            }

            public override Encoding Encoding
            {
                get { return Encoding.ASCII; }
            }

            public override void WriteLine(string message)
            {
                _output.WriteLine(message);
            }

            public override void WriteLine(string format, params object[] args)
            {
                _output.WriteLine(format, args);
            }

            public override void Write(char value)
            {
                throw new System.Exception("This text writer only supports WriteLine(string) and WriteLine(string, params object[]).");
            }
        }

    }
}
