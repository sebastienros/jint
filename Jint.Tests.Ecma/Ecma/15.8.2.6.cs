using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.6")]
        public void IfXIsNanMathCeilXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.6/S15.8.2.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.6")]
        public void IfXIs0MathCeilXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.6/S15.8.2.6_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.6")]
        public void IfXIs0MathCeilXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.6/S15.8.2.6_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.6")]
        public void IfXIsInfinityMathCeilXIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.6/S15.8.2.6_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.6")]
        public void IfXIsInfinityMathCeilXIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.6/S15.8.2.6_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.6")]
        public void IfXIsLessThan0ButGreaterThan1MathCeilXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.6/S15.8.2.6_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.6")]
        public void TheValueOfMathCeilXIsTheSameAsTheValueOfMathFloorX()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.6/S15.8.2.6_A7.js", false);
        }


    }
}
