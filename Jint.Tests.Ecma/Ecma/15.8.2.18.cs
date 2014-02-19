using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_18 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.18")]
        public void IfXIsNanMathTanXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.18/S15.8.2.18_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.18")]
        public void IfXIs0MathTanXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.18/S15.8.2.18_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.18")]
        public void IfXIs0MathTanXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.18/S15.8.2.18_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.18")]
        public void IfXIsInfinityMathTanXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.18/S15.8.2.18_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.18")]
        public void IfXIsInfinityMathTanXIsNan2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.18/S15.8.2.18_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.18")]
        public void TangentIsAPeriodicFunctionWithPeriodPi()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.18/S15.8.2.18_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.18")]
        public void MathTanRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.18/S15.8.2.18_A7.js", false);
        }


    }
}
