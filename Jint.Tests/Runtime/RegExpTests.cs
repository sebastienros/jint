using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Array;

namespace Jint.Tests.Runtime
{
    public class RegExpTests
    {
        private readonly string testRegex = "^(https?:\\/\\/)?([\\da-z\\.-]+)\\.([a-z\\.]{2,6})([\\/\\w\\.-]*)*\\/?$";
        private readonly string testedValue = "https://archiverbx.blob.core.windows.net/static/C:/Users/USR/Documents/Projects/PROJ/static/images/full/1234567890.jpg";

        [Fact]
        public void CanNotBreakEngineWithLongRunningMatch()
        {
            var engine = new Engine(e => e.RegexTimeoutInterval(TimeSpan.FromSeconds(1)));

            Assert.Throws<RegexMatchTimeoutException>(() =>
            {
                engine.Execute($"'{testedValue}'.match(/{testRegex}/)");
            });
        }

        [Fact]
        public void CanNotBreakEngineWithLongRunningRegExp()
        {
            var engine = new Engine(e => e.RegexTimeoutInterval(TimeSpan.FromSeconds(1)));

            Assert.Throws<RegexMatchTimeoutException>(() =>
            {
               engine.Execute($"'{testedValue}'.match(new RegExp(/{testRegex}/))");
            });
        }

        [Fact]
        public void PreventsInfiniteLoop()
        {
            var engine = new Engine();
            var result = (ArrayInstance)engine.Evaluate("'x'.match(/|/g);");
            Assert.Equal((uint) 2, result.Length);
            Assert.Equal("", result[0]);
            Assert.Equal("", result[1]);
        }

        [Fact]
        public void ToStringWithNonRegExpInstanceAndMissingProperties()
        {
            var engine = new Engine();
            var result = engine.Evaluate("/./['toString'].call({})").AsString();

            Assert.Equal("/undefined/undefined", result);
        }

        [Fact]
        public void ToStringWithNonRegExpInstanceAndValidProperties()
        {
            var engine = new Engine();
            var result = engine.Evaluate("/./['toString'].call({ source: 'a', flags: 'b' })").AsString();

            Assert.Equal("/a/b", result);
        }

        [Fact]
        public void MatchAllIteratorReturnsCorrectNumberOfElements()
        {
            var engine = new Engine();
            var result = engine.Evaluate("[...'one two three'.matchAll(/t/g)].length").AsInteger();
            
            Assert.Equal(2, result);
        }

        [Fact]
        public void ToStringWithRealRegExpInstance()
        {
            var engine = new Engine();
            var result = engine.Evaluate("/./['toString'].call(/test/g)").AsString();

            Assert.Equal("/test/g", result);
        }

        [Fact]
        public void ShouldNotThrowErrorOnIncompatibleRegex()
        {
            var engine = new Engine();
            Assert.NotNull(engine.Evaluate(@"/[^]*?(:[rp][el]a[\w-]+)[^]*/"));
            Assert.NotNull(engine.Evaluate("/[^]a/"));
            Assert.NotNull(engine.Evaluate("new RegExp('[^]a')"));

            Assert.NotNull(engine.Evaluate("/[]/"));
            Assert.NotNull(engine.Evaluate("new RegExp('[]')"));
        }

        [Fact]
        public void ShouldNotThrowErrorOnRegExNumericNegation()
        {
            var engine = new Engine();
            Assert.True(ReferenceEquals(JsNumber.DoubleNaN, engine.Evaluate("-/[]/")));
        }
    }
}
