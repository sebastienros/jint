using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_4_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.4.8")]
        public void WhiteSpaceAndLineTerminatorBetweenAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXReturnsToint32X()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXReturnsToint32X2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXReturnsToint32X3()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXReturnsToint32X4()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.8")]
        public void OperatorXReturnsToint32X5()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.8/S11.4.8_A3_T5.js", false);
        }


    }
}
