using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.5")]
        public void MathLog10EIsApproximately04342944819032518()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.5/S15.8.1.5_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.5")]
        public void ValuePropertyLog10EOfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.5/S15.8.1.5_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.5")]
        public void ValuePropertyLog10EOfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.5/S15.8.1.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.5")]
        public void ValuePropertyLog10EOfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.5/S15.8.1.5_A4.js", false);
        }


    }
}
