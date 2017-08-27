using System;
using Esprima;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class ErrorTests
    {
        [Fact]
        public void CanReturnCorrectErrorMessageAndLocation1()
        {
            var script = @"
var a = {};

var b = a.user.name;
";

            var engine = new Engine();
            var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
            Assert.Equal("user is undefined", e.Message);
            Assert.Equal(4, e.Location.Start.Line);
            Assert.Equal(8, e.Location.Start.Column);
        }

        [Fact]
        public void CanReturnCorrectErrorMessageAndLocation2()
        {
            var script = @"
 test();
";

            var engine = new Engine();
            var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
            Assert.Equal("test is not defined", e.Message);
            Assert.Equal(2, e.Location.Start.Line);
            Assert.Equal(1, e.Location.Start.Column);
        }

        [Fact]
        public void CanProduceCorrectStackTrace()
        {
            var engine = new Engine(options => options.LimitRecursion(1000));

            engine.Execute(@"var a = function(v) {
	return v.xxx.yyy;
}

var b = function(v) {
	return a(v);
}", new ParserOptions("custom.js"));

            var e = Assert.Throws<JavaScriptException>(() => engine.Execute("var x = b(7);", new ParserOptions("main.js")));
            Assert.Equal("xxx is undefined", e.Message);
            Assert.Equal(2, e.Location.Start.Line);
            Assert.Equal(8, e.Location.Start.Column);
            Assert.Equal("custom.js", e.Location.Source);

            var stack = e.CallStack;
            Assert.Equal(@" at a(v) @ custom.js 8:6
 at b(7) @ main.js 8:1
".Replace("\r\n", "\n"), stack.Replace("\r\n", "\n"));
        }
    }
}