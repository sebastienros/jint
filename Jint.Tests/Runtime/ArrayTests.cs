using System;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class ArrayTests
    {
        private readonly Engine _engine;

        public ArrayTests()
        {
            _engine = new Engine()
                    .SetValue("log", new Action<object>(Console.WriteLine))
                    .SetValue("assert", new Action<bool>(Assert.True))
                    .SetValue("equal", new Action<object, object>(Assert.Equal));
        }

        private void RunTest(string source)
        {
            _engine.Execute(source);
        }


        [Fact]
        public void ArrayPrototypeToStringWithArray()
        {
            var result = _engine.Execute("Array.prototype.toString.call([1,2,3]);").GetCompletionValue().AsString();

            Assert.Equal("1,2,3", result);
        }

        [Fact]
        public void ArrayPrototypeToStringWithNumber()
        {
            var result = _engine.Execute("Array.prototype.toString.call(1);").GetCompletionValue().AsString();

            Assert.Equal("[object Number]", result);
        }

        [Fact]
        public void ArrayPrototypeToStringWithObject()
        {
            var result = _engine.Execute("Array.prototype.toString.call({});").GetCompletionValue().AsString();

            Assert.Equal("[object Object]", result);
        }

    }
}