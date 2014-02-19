using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_10 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.10")]
        public void IfXIsNanMathLogXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.10/S15.8.2.10_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.10")]
        public void IfXIsLessThan0MathLogXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.10/S15.8.2.10_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.10")]
        public void IfXIs0Or0MathLogXIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.10/S15.8.2.10_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.10")]
        public void IfXIs1MathLogXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.10/S15.8.2.10_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.10")]
        public void IfXIsInfinityMathLogXIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.10/S15.8.2.10_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.10")]
        public void MathLogRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.10/S15.8.2.10_A6.js", false);
        }


    }
}
