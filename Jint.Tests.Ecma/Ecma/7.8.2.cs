using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_8_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.8.2")]
        public void LiteralBooleanliteral()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.2/S7.8.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.2")]
        public void LiteralBooleanliteral2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.2/S7.8.2_A1_T2.js", false);
        }


    }
}
