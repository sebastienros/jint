using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.7")]
        public void IfXIsNanMathCosXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.7/S15.8.2.7_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.7")]
        public void IfXIs0MathCosXIs1()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.7/S15.8.2.7_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.7")]
        public void IfXIs0MathCosXIs12()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.7/S15.8.2.7_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.7")]
        public void IfXIsInfinityMathCosXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.7/S15.8.2.7_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.7")]
        public void IfXIsInfinityMathCosXIsNan2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.7/S15.8.2.7_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.7")]
        public void CosineIsAPeriodicFunctionWithPeriod2Pi()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.7/S15.8.2.7_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.7")]
        public void MathCosItIsRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.7/S15.8.2.7_A7.js", false);
        }


    }
}
