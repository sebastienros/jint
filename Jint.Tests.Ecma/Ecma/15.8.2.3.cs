using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.3")]
        public void IfXIsNanMathAsinXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.3/S15.8.2.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.3")]
        public void IfXIsGreaterThan1MathAsinXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.3/S15.8.2.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.3")]
        public void IfXIsLessThan1MathAsinXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.3/S15.8.2.3_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.3")]
        public void IfXIs0MathAsinXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.3/S15.8.2.3_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.3")]
        public void IfXIs0MathAsinXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.3/S15.8.2.3_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.3")]
        public void MathAsinRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.3/S15.8.2.3_A6.js", false);
        }


    }
}
