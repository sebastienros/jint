using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_15 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.15")]
        public void IfXIsNanMathRoundXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.15")]
        public void IfXIs0MathRoundXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.15")]
        public void IfXIs0MathRoundXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.15")]
        public void IfXIsInfinityMathRoundXIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.15")]
        public void IfXIsInfinityMathRoundXIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.15")]
        public void IfXIsEqualTo0OrGreaterThan0OrIfXIsLessThan05MathRoundXIsEqualToMathFloorX05()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.15")]
        public void IfXIsLessThanOrEqualTo0AndXIsGreaterThanOrEqualTo05MathRoundXIsEqualTo0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.15/S15.8.2.15_A7.js", false);
        }


    }
}
