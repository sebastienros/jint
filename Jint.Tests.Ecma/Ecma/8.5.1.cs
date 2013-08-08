using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_5_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.5.1")]
        public void ValidNumberRanges()
        {
			RunTest(@"TestCases/ch08/8.5/8.5.1.js", false);
        }


    }
}
