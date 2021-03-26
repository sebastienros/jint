using Jint.Native.Object;
using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class DebugHandlerTests
    {
        [Fact]
        public void AvoidsPauseRecursion()
        {
            // While the DebugHandler is in a paused state, it shouldn't relay further OnStep calls to Break/Step.
            // Such calls would occur e.g. if Step/Break event handlers evaluate accessors. Failing to avoid
            // reentrance in a multithreaded environment (e.g. using ManualResetEvent(Slim)) would cause
            // a deadlock.
            string script = @"
                const obj = { get name() { 'fail'; return 'Smith'; } };
                'target';
            ";

            var engine = new Engine(options => options.DebugMode());

            bool didPropertyAccess = false;

            engine.Step += (sender, info) =>
            {
                // We should never reach "fail", because the only way it's executed is from
                // within this Step handler
                Assert.False(info.ReachedLiteral("fail"));

                if (info.ReachedLiteral("target"))
                {
                    var obj = info.Scopes.Global["obj"] as ObjectInstance;
                    var prop = obj.GetOwnProperty("name");
                    // This is where reentrance would occur:
                    var value = prop.Get.Invoke();
                    didPropertyAccess = true;
                }
                return StepMode.Into;
            };

            engine.Execute(script);

            Assert.True(didPropertyAccess);
        }
    }
}
