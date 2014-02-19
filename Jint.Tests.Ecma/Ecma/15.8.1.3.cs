using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.3")]
        public void MathLn2IsApproximately06931471805599453()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.3/S15.8.1.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.3")]
        public void ValuePropertyLn2OfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.3/S15.8.1.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.3")]
        public void ValuePropertyLn2OfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.3/S15.8.1.3_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.3")]
        public void ValuePropertyLn2OfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.3/S15.8.1.3_A4.js", false);
        }


    }
}
