using Jint.Runtime;

namespace Jint.Tests.Runtime
{
    public class SetTests
    {
        [Fact]
        public void ShouldThrowWhenCalledWithoutNew()
        {
            var e = Assert.Throws<JavaScriptException>(() => new Engine().Execute("const m = new Set(); Set.call(m,[]);"));
            Assert.Equal("Constructor Set requires 'new'", e.Message);
        }
    }
}
