using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_17 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.17")]
        public void IfXIsNanMathSqrtXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.17/S15.8.2.17_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.17")]
        public void IfXLessThan0MathSqrtXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.17/S15.8.2.17_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.17")]
        public void IfXIsEqualTo0MathSqrtXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.17/S15.8.2.17_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.17")]
        public void IfXIsEqualTo0MathSqrtXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.17/S15.8.2.17_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.17")]
        public void IfXIsEqualToInfinityMathSqrtXIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.17/S15.8.2.17_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.17")]
        public void MathSqrtRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.17/S15.8.2.17_A6.js", false);
        }


    }
}
