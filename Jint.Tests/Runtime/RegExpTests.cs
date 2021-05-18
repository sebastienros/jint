using System;
using System.Text.RegularExpressions;
using Jint.Native.Array;
using Xunit;

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
        public void ToStringWithRealRegExpInstance()
        {
            var engine = new Engine();
            var result = engine.Evaluate("/./['toString'].call(/test/g)").AsString();

            Assert.Equal("/test/g", result);
        }
    }
}
