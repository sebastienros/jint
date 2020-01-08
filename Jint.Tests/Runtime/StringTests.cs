using System;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class StringTests
    {
        public StringTests()
        {
            _engine = new Engine()
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal));
        }

        private readonly Engine _engine;

        [Fact]
        public void StringConcatenationAndReferences()
        {
            const string script = @"
var foo = 'foo';
foo += 'foo';
var bar = foo;
bar += 'bar';
";
            var value = _engine.Execute(script);
            var foo = _engine.Execute("foo").GetCompletionValue().AsString();
            var bar = _engine.Execute("bar").GetCompletionValue().AsString();
            Assert.Equal("foofoo", foo);
            Assert.Equal("foofoobar", bar);
        }
    }
}