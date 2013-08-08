using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.8")]
        public void MathSqrt2IsApproximately14142135623730951()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.8/S15.8.1.8_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.8")]
        public void ValuePropertySqrt2OfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.8/S15.8.1.8_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.8")]
        public void ValuePropertySqrt2OfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.8/S15.8.1.8_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.8")]
        public void ValuePropertySqrt2OfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.8/S15.8.1.8_A4.js", false);
        }


    }
}
