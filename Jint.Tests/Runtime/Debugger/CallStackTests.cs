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
    }
}
