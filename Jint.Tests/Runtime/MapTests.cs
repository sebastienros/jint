using Jint.Runtime;

namespace Jint.Tests.Runtime
{
    public class MapTests
    {
        [Fact]
        public void ShouldThrowWhenCalledWithoutNew()
        {
            var e = Assert.Throws<JavaScriptException>(() => new Engine().Execute("const m = new Map(); Map.call(m,[]);"));
            Assert.Equal("Constructor Map requires 'new'", e.Message);
        }
    }
}
