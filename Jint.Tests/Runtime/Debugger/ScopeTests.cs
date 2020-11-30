using Jint.Native;
using Jint.Runtime.Debugger;
using System;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class ScopeTests
    {
        /// <summary>
        /// Initializes engine in debugmode and executes script until debugger statement,
        /// before calling stepHandler for assertions. Also asserts that a break was triggered.
        /// </summary>
        /// <param name="script">Script that is basis for testing</param>
        /// <param name="breakHandler">Handler for assertions</param>
        private void TestDebugInformation(string script, Action<DebugInformation> breakHandler)
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint)
            );
            
            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                breakHandler(info);
                return StepMode.None;
            };

            engine.Execute(script);
            
            Assert.True(didBreak);
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalConst()
        {
            string script = @"
                const globalConstant = 'test';
                debugger;
            ";

            TestDebugInformation(script, info =>
            {
                var variable = Assert.Single(info.Globals, g => g.Key == "globalConstant");
                Assert.Equal("test", variable.Value.AsString());

                variable = Assert.Single(info.Locals, g => g.Key == "globalConstant");
                Assert.Equal("test", variable.Value.AsString());
            });
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalLet()
        {
            string script = @"
                let globalLet = 'test';
                debugger;";

            TestDebugInformation(script, info =>
            {
                var variable = Assert.Single(info.Globals, g => g.Key == "globalLet");
                Assert.Equal("test", variable.Value.AsString());

                variable = Assert.Single(info.Locals, g => g.Key == "globalLet");
                Assert.Equal("test", variable.Value.AsString());
            });
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalVar()
        {
            string script = @"
                var globalVar = 'test';
                debugger;";

            TestDebugInformation(script, info =>
            {
                var variable = Assert.Single(info.Globals, g => g.Key == "globalVar");
                Assert.Equal("test", variable.Value.AsString());

                variable = Assert.Single(info.Locals, g => g.Key == "globalVar");
                Assert.Equal("test", variable.Value.AsString());
            });
        }

        [Fact]
        public void OnlyLocalsIncludeLocalConst()
        {
            string script = @"
                function test()
                {
                    const localConst = 'test';
                    debugger;
                }
                test();";

            TestDebugInformation(script, info =>
            {
                var variable = Assert.Single(info.Locals, g => g.Key == "localConst");
                Assert.Equal("test", variable.Value.AsString());
                Assert.DoesNotContain(info.Globals, g => g.Key == "localConst");
            });
        }

        [Fact]
        public void OnlyLocalsIncludeLocalLet()
        {
            string script = @"
                function test()
                {
                    let localLet = 'test';
                    debugger;
                }
                test();";

            TestDebugInformation(script, info =>
            {
                var variable = Assert.Single(info.Locals, g => g.Key == "localLet");
                Assert.Equal("test", variable.Value.AsString());
                Assert.DoesNotContain(info.Globals, g => g.Key == "localLet");
            });
        }

        [Fact]
        public void OnlyLocalsIncludeLocalVar()
        {
            string script = @"
                function test()
                {
                    var localVar = 'test';
                    debugger;
                }
                test();";

            TestDebugInformation(script, info =>
            {
                var variable = Assert.Single(info.Locals, g => g.Key == "localVar");
                Assert.Equal("test", variable.Value.AsString());
                Assert.DoesNotContain(info.Globals, g => g.Key == "localVar");
            });
        }

        [Fact]
        public void BlockScopedVariablesAreInvisibleOutsideBlock()
        {
            string script = @"
            debugger;
            {
                let blockLet = 'block';
                const blockConst = 'block';
            }";

            TestDebugInformation(script, info =>
            {
                Assert.DoesNotContain(info.Locals, g => g.Key == "blockLet");
                Assert.DoesNotContain(info.Locals, g => g.Key == "blockConst");
            });
        }

        [Fact]
        public void BlockScopedConstIsVisibleInsideBlock()
        {
            string script = @"
            'dummy statement';
            {
                debugger; // const is initialized (as undefined) at beginning of block
                const blockConst = 'block';
            }";

            TestDebugInformation(script, info =>
            {
                Assert.Single(info.Locals, c => c.Key == "blockConst" && c.Value == JsUndefined.Undefined);
            });
        }

        [Fact]
        public void BlockScopedLetIsVisibleInsideBlock()
        {
            string script = @"
            'dummy statement';
            {
                let blockLet = 'block';
                debugger; // let isn't initialized until declaration
            }";

            TestDebugInformation(script, info =>
            {
                Assert.Single(info.Locals, v => v.Key == "blockLet" && v.Value.AsString() == "block");
            });
        }
    }
}
