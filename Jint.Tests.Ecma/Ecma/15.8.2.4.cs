using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.4")]
        public void IfXIsNanMathAtanXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.4/S15.8.2.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.4")]
        public void IfXIs0MathAtanXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.4/S15.8.2.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.4")]
        public void IfXIs0MathAtanXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.4/S15.8.2.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.4")]
        public void IfXIsInfinityMathAtanXIsAnImplementationDependentApproximationToPi2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.4/S15.8.2.4_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.4")]
        public void IfXIsInfinityMathAtanXIsAnImplementationDependentApproximationToPi22()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.4/S15.8.2.4_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.4")]
        public void MathAtanRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.4/S15.8.2.4_A6.js", false);
        }


    }
}
