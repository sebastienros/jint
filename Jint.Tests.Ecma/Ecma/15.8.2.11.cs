using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.11")]
        public void MathMaxIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.11/15.8.2.11-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.11")]
        public void IfNoArgumentsAreGivenMathMaxIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.11/S15.8.2.11_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.11")]
        public void IfAnyValueIsNanTheResultOfMathMaxIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.11/S15.8.2.11_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.11")]
        public void IsConsideredToBeLargerThan0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.11/S15.8.2.11_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.11")]
        public void TheLengthPropertyOfTheMathMaxMethodIs2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.11/S15.8.2.11_A4.js", false);
        }


    }
}
