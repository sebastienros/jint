using Jint.Runtime;

namespace Jint.Tests.Runtime
{
    public class NumberTests
    {
        private readonly Engine _engine;

        public NumberTests()
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

        [Theory]
        [InlineData(1, "3.0e+0")]
        [InlineData(50, "3.00000000000000000000000000000000000000000000000000e+0")]
        [InlineData(100, "3.0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000e+0")]
        public void ToExponential(int fractionDigits, string result)
        {
            var value = _engine.Evaluate($"(3).toExponential({fractionDigits}).toString()").AsString();
            Assert.Equal(result, value);
        }

        [Theory]
        [InlineData(1, "3.0")]
        [InlineData(50, "3.00000000000000000000000000000000000000000000000000")]
        [InlineData(99, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
        public void ToFixed(int fractionDigits, string result)
        {
            var value = _engine.Evaluate($"(3).toFixed({fractionDigits}).toString()").AsString();
            Assert.Equal(result, value);
        }

        [Fact]
        public void ToFixedWith100FractionDigitsThrows()
        {
            var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate($"(3).toFixed(100)"));
            Assert.Equal("100 fraction digits is not supported due to .NET format specifier limitation", ex.Message);
        }

        [Theory]
        [InlineData(1, "3")]
        [InlineData(50, "3.0000000000000000000000000000000000000000000000000")]
        [InlineData(100, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
        public void ToPrecision(int fractionDigits, string result)
        {
            var value = _engine.Evaluate($"(3).toPrecision({fractionDigits}).toString()").AsString();
            Assert.Equal(result, value);
        }
    }
}
