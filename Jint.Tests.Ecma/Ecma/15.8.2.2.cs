using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.2")]
        public void IfXIsNanMathAcosXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.2/S15.8.2.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.2")]
        public void IfXIsGreaterThan1MathAcosXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.2/S15.8.2.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.2")]
        public void IfXIsLessThan1MathAcosXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.2/S15.8.2.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.2")]
        public void IfXIsExactly1MathAcosXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.2/S15.8.2.2_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.2")]
        public void MathAcosRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.2/S15.8.2.2_A5.js", false);
        }


    }
}
