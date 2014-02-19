using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_16 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.16")]
        public void IfXIsNanMathSinXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.16/S15.8.2.16_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.16")]
        public void IfXIs0MathSinXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.16/S15.8.2.16_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.16")]
        public void IfXIsInfinityMathSinXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.16/S15.8.2.16_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.16")]
        public void IfXIsInfinityMathSinXIsNan2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.16/S15.8.2.16_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.16")]
        public void SineIsAPeriodicFunctionWithPeriod2Pi()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.16/S15.8.2.16_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.16")]
        public void MathSinItIsRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.16/S15.8.2.16_A7.js", false);
        }


    }
}
