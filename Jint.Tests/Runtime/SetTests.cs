using Jint.Runtime;
using Xunit;

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

        [Fact]
        public void SerializableEvaluated()
        {
            var internally = new Engine().Evaluate("JSON.stringify(new Set())");
            Assert.Equal("{}", internally);
            var externally = JsonConvert.SerializeObject(new Engine().Evaluate("new Set()"));
            Assert.Equal("{}", externally);
        }
    }
}
