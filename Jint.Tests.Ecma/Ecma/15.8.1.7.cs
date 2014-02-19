using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.7")]
        public void MathSqrt12IsApproximately07071067811865476()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.7/S15.8.1.7_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.7")]
        public void ValuePropertySqrt12OfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.7/S15.8.1.7_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.7")]
        public void ValuePropertySqrt12OfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.7/S15.8.1.7_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.7")]
        public void ValuePropertySqrt12OfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.7/S15.8.1.7_A4.js", false);
        }


    }
}
