using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.8")]
        public void IfXIsNanMathExpXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.8/S15.8.2.8_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.8")]
        public void IfXIs0MathExpXIs1()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.8/S15.8.2.8_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.8")]
        public void IfXIs0MathExpXIs12()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.8/S15.8.2.8_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.8")]
        public void IfXIsInfinityMathExpXIsIfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.8/S15.8.2.8_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.8")]
        public void IfXIsInfinityMathExpXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.8/S15.8.2.8_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.8")]
        public void MathExpRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.8/S15.8.2.8_A6.js", false);
        }


    }
}
