using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.1")]
        public void IfXIsNanMathAbsXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.1/S15.8.2.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.1")]
        public void IfXIs0MathAbsXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.1/S15.8.2.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.1")]
        public void IfXIsInfinityMathAbsXIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.1/S15.8.2.1_A3.js", false);
        }


    }
}
