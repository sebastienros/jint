using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.9")]
        public void IfXIsNanMathFloorXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.9/S15.8.2.9_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.9")]
        public void IfXIs0MathFloorXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.9/S15.8.2.9_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.9")]
        public void IfXIs0MathFloorXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.9/S15.8.2.9_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.9")]
        public void IfXIsInfinityMathFloorXIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.9/S15.8.2.9_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.9")]
        public void IfXIsInfinityMathFloorXIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.9/S15.8.2.9_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.9")]
        public void IfXIsGreaterThan0ButLessThan1MathFloorXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.9/S15.8.2.9_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.9")]
        public void TheValueOfMathFloorXIsTheSameAsTheValueOfMathCeilX()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.9/S15.8.2.9_A7.js", false);
        }


    }
}
