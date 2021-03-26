using Jint.Runtime.Debugger;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class CallStackTests
    {
        [Fact]
        public void NamesRegularFunction()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("regularFunction", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"function regularFunction() { debugger; }
                regularFunction()");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesFunctionExpression()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("functionExpression", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"const functionExpression = function() { debugger; }
                functionExpression()");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesNamedFunctionExpression()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("namedFunction", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"const functionExpression = function namedFunction() { debugger; }
                functionExpression()");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesArrowFunction()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("arrowFunction", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"const arrowFunction = () => { debugger; }
                arrowFunction()");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesNewFunction()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                // Ideally, this should be "(anonymous)", but FunctionConstructor sets the "anonymous" name.
                Assert.Equal("anonymous", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"const newFunction = new Function('debugger;');
                newFunction()");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesMemberFunction()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("memberFunction", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"const obj = { memberFunction() { debugger; } };
                obj.memberFunction()");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesAnonymousFunction()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("(anonymous)", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"(function()
                {
                    debugger;
                }());");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesGetAccessor()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("get accessor", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"
                const obj = {
                    get accessor()
                    {
                        debugger;
                        return 'test';
                    }
                };
                const x = obj.accessor;");

            Assert.True(didBreak);
        }

        [Fact]
        public void NamesSetAccessor()
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("set accessor", info.CurrentCallFrame.FunctionName);
                return StepMode.None;
            };

            engine.Execute(
                @"
                const obj = {
                    set accessor(value)
                    {
                        debugger;
                        this.value = value;
                    }
                };
                obj.accessor = 42;");

            Assert.True(didBreak);
        }
    }
}
