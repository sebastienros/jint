using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("regularFunction", info.CallStack.Peek());
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
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("functionExpression", info.CallStack.Peek());
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
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("namedFunction", info.CallStack.Peek());
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
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("arrowFunction", info.CallStack.Peek());
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
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                // Ideally, this should be "(anonymous)", but FunctionConstructor sets the "anonymous" name.
                Assert.Equal("anonymous", info.CallStack.Peek());
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
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("memberFunction", info.CallStack.Peek());
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
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                Assert.Equal("(anonymous)", info.CallStack.Peek());
                return StepMode.None;
            };

            engine.Execute(
                @"(function()
                {
                    debugger;
                }());");

            Assert.True(didBreak);
        }
    }
}
