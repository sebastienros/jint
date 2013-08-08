using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.12")]
        public void MathMinIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.12/15.8.2.12-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.12")]
        public void IfNoArgumentsAreGivenMathMinIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.12/S15.8.2.12_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.12")]
        public void IfAnyValueIsNanTheResultOfMathMinIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.12/S15.8.2.12_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.12")]
        public void IsConsideredToBeLargerThan0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.12/S15.8.2.12_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.12")]
        public void TheLengthPropertyOfTheMathMinMethodIs2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.12/S15.8.2.12_A4.js", false);
        }


    }
}
