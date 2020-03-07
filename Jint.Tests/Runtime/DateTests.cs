using System;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class DateTests
    {
        private readonly Engine _engine;

        public DateTests()
        {
            _engine = new Engine()
                    .SetValue("log", new Action<object>(Console.WriteLine))
                    .SetValue("assert", new Action<bool>(Assert.True))
                    .SetValue("equal", new Action<object, object>(Assert.Equal));
        }

        [Fact]
        public void NaNToString()
        {
            var value = _engine.Execute("new Date(NaN).toString();").GetCompletionValue().AsString();
            Assert.Equal("Invalid Date", value);
        }

        [Fact]
        public void NaNToDateString()
        {
            var value = _engine.Execute("new Date(NaN).toDateString();").GetCompletionValue().AsString();
            Assert.Equal("Invalid Date", value);
        }

        [Fact]
        public void NaNToTimeString()
        {
            var value = _engine.Execute("new Date(NaN).toTimeString();").GetCompletionValue().AsString();
            Assert.Equal("Invalid Date", value);
        }

        [Fact]
        public void NaNToLocaleString()
        {
            var value = _engine.Execute("new Date(NaN).toLocaleString();").GetCompletionValue().AsString();
            Assert.Equal("Invalid Date", value);
        }

        [Fact]
        public void NaNToLocaleDateString()
        {
            var value = _engine.Execute("new Date(NaN).toLocaleDateString();").GetCompletionValue().AsString();
            Assert.Equal("Invalid Date", value);
        }

        [Fact]
        public void NaNToLocaleTimeString()
        {
            var value = _engine.Execute("new Date(NaN).toLocaleTimeString();").GetCompletionValue().AsString();
            Assert.Equal("Invalid Date", value);
        }

    }
}