using Jint.Native;
using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static Jint.Engine;

namespace Jint.Tests.Runtime.Debugger
{
    public class ScopeTests
    {
        /// <summary>
        /// Initializes engine in debugmode and executes script through a given number of steps, before calling stepHandler for assertions.
        /// </summary>
        /// <param name="script">Script that is basis for testing</param>
        /// <param name="stepHandler">Handler for assertions</param>
        /// <param name="steps">Number of steps to execute before calling handler</param>
        private void TestStep(string script, Action<DebugInformation> stepHandler, int steps = 0)
        {
            var engine = new Engine(options => options.DebugMode(true));
            int stepsRaised = 0;
            engine.Step += (sender, info) =>
            {
                stepsRaised++;
                if (stepsRaised > steps)
                {
                    stepHandler(info);
                    return StepMode.None;
                }
                return StepMode.Into;
            };
            engine.Execute(script);
            Assert.Equal(steps + 1, stepsRaised);
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalConst()
        {
            TestStep(
                "const globalConstant = 'test';",
                info =>
                {
                    var variable = Assert.Single(info.Globals, g => g.Key == "globalConstant");
                    // consts are undefined before initialization
                    Assert.Equal(JsUndefined.Undefined, variable.Value);

                    variable = Assert.Single(info.Locals, g => g.Key == "globalConstant");
                    Assert.Equal(JsUndefined.Undefined, variable.Value);
                }
            );
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalLet()
        {
            // lets don't exist before initialization, so we need to step past the declaration
            TestStep(
                @"
                    let globalLet = 'test';
                    'dummy'; // Dummy statement to avoid stepping out of script
                ",
                info =>
                {
                    var variable = Assert.Single(info.Globals, g => g.Key == "globalLet");
                    Assert.Equal("test", variable.Value.AsString());

                    variable = Assert.Single(info.Locals, g => g.Key == "globalLet");
                    Assert.Equal("test", variable.Value.AsString());
                },
                steps: 1
            );
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalVar()
        {
            TestStep(
                "var globalVar = 'test';",
                info =>
                {
                    var variable = Assert.Single(info.Globals, g => g.Key == "globalVar");
                    // vars are undefined before initialization
                    Assert.Equal(JsUndefined.Undefined, variable.Value);
                    
                    variable = Assert.Single(info.Locals, g => g.Key == "globalVar");
                    Assert.Equal(JsUndefined.Undefined, variable.Value);
                }
            );
        }

        [Fact]
        public void OnlyLocalsIncludeLocalConst()
        {
            string script = @"
                function test()
                {
                    const localConst = 'test';
                    'dummy'; // Dummy statement to avoid stepping out of script  
                }
                test();";

            TestStep(
                script,
                info =>
                {
                    var variable = Assert.Single(info.Locals, g => g.Key == "localConst");
                    Assert.Equal("test", variable.Value.AsString());
                    Assert.DoesNotContain(info.Globals, g => g.Key == "localConst");
                },
                steps: 4
            );
        }

        [Fact]
        public void OnlyLocalsIncludeLocalLet()
        {
            string script = @"
                function test()
                {
                    let localLet = 'test';
                    'dummy'; // Dummy statement to avoid stepping out of script  
                }
                test();";

            TestStep(
                script,
                info =>
                {
                    var variable = Assert.Single(info.Locals, g => g.Key == "localLet");
                    Assert.Equal("test", variable.Value.AsString());
                    Assert.DoesNotContain(info.Globals, g => g.Key == "localLet");
                },
                steps: 4
            );
        }

        [Fact]
        public void OnlyLocalsIncludeLocalVar()
        {
            string script = @"
                function test()
                {
                    var localVar = 'test';
                    'dummy'; // Dummy statement to avoid stepping out of script  
                }
                test();";

            TestStep(
                script,
                info =>
                {
                    var variable = Assert.Single(info.Locals, g => g.Key == "localVar");
                    Assert.Equal("test", variable.Value.AsString());
                    Assert.DoesNotContain(info.Globals, g => g.Key == "localVar");
                },
                steps: 4
            );
        }

        [Fact]
        public void BlockScopedVariablesAreOnlyVisibleInsideBlock()
        {
            string script = @"
            'dummy';
            'dummy';
            {
                let blockLet = 'block';
                const blockConst = 'block';
                'dummy';
            }
";

            TestStep(
                script,
                info =>
                {
                    Assert.DoesNotContain(info.Locals, g => g.Key == "blockLet");
                    Assert.DoesNotContain(info.Locals, g => g.Key == "blockConst");
                },
                steps: 1
            );

            TestStep(
                script,
                info =>
                {
                    Assert.Single(info.Locals, v => v.Key == "blockLet" && v.Value.AsString() == "block");
                    Assert.Single(info.Locals, c => c.Key == "blockConst" && c.Value.AsString() == "block");
                },
                steps: 5
            );
        }
    }
}
