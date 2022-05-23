using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class MapTests
    {
        [Fact]
        public void ShouldThrowWhenCalledWithoutNew()
        {
            var e = Assert.Throws<EvaluationException>(() => new Engine().Execute("const m = new Map(); Map.call(m,[]);"));
            Assert.Equal("Constructor Map requires 'new'", e.Message);
        }
    }
}