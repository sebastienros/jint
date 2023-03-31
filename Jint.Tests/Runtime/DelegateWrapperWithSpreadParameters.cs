using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime
{
    public class DelegateWrapperWithSpreadParameters
    {
        [Fact]
        public void Test()
        {
            var engine = new Engine();
            engine.SetValue("registerCallback", new DelegateWrapper(engine, new RegisterCallbackDelegate(RegisterCallback), true));

            engine.Execute(@"
                var argsConcat = '';
                registerCallback((valTest, valOther, valNumber) => {
                    argsConcat+=typeof valTest;
                    argsConcat+=' ' + typeof valOther;
                    argsConcat+=' ' + typeof valNumber;
                }, 'test', 'other', 1337);
            ");

            Assert.True(engine.Evaluate("argsConcat == 'string string number'").AsBoolean());
        }

        private static void RegisterCallback(CallbackAction callback, params object[] arguments)
        {
            callback.Invoke(arguments);
        }

        private delegate void RegisterCallbackDelegate(CallbackAction callback, params object[] arguments);

        private delegate void CallbackAction(params object[] arguments);

    }
}
