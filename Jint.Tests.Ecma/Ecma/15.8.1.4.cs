using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.4")]
        public void MathLog2EIsApproximately14426950408889634()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.4/S15.8.1.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.4")]
        public void ValuePropertyLog2EOfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.4/S15.8.1.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.4")]
        public void ValuePropertyLog2EOfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.4/S15.8.1.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.4")]
        public void ValuePropertyLog2EOfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.4/S15.8.1.4_A4.js", false);
        }


    }
}
