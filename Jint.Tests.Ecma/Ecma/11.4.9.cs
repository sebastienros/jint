using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_4_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.4.9")]
        public void WhiteSpaceAndLineTerminatorBetweenAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXReturnsTobooleanX()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXReturnsTobooleanX2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXReturnsTobooleanX3()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXReturnsTobooleanX4()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.9")]
        public void OperatorXReturnsTobooleanX5()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.9/S11.4.9_A3_T5.js", false);
        }


    }
}
