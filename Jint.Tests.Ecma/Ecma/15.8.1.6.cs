using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.6")]
        public void MathPiIsApproximately31415926535897932()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.6/S15.8.1.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.6")]
        public void ValuePropertyPiOfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.6/S15.8.1.6_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.6")]
        public void ValuePropertyPiOfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.6/S15.8.1.6_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.6")]
        public void ValuePropertyPiOfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.6/S15.8.1.6_A4.js", false);
        }


    }
}
