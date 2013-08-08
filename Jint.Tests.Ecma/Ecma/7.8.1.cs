using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_8_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.8.1")]
        public void LiteralNullliteral()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.1/S7.8.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.1")]
        public void LiteralNullliteral2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.1/S7.8.1_A1_T2.js", false);
        }


    }
}
