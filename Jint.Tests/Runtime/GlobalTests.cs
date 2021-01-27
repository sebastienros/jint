using Xunit;

namespace Jint.Tests.Runtime
{
    public class GlobalTests
    {
        [Fact]
        public void UnescapeAtEndOfString()
        {
            var e = new Engine();

            Assert.Equal("@", e.Execute("unescape('%40');").GetCompletionValue().AsString());
            Assert.Equal("@_", e.Execute("unescape('%40_');").GetCompletionValue().AsString());
            Assert.Equal("@@", e.Execute("unescape('%40%40');").GetCompletionValue().AsString());
            Assert.Equal("@", e.Execute("unescape('%u0040');").GetCompletionValue().AsString());
            Assert.Equal("@@", e.Execute("unescape('%u0040%u0040');").GetCompletionValue().AsString());
        }
    }
}