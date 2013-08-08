using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_4_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.4.6")]
        public void ShouldBeZero()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/11.4.6-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void WhiteSpaceAndLineTerminatorBetweenAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXReturnsTonumberX()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXReturnsTonumberX2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXReturnsTonumberX3()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXReturnsTonumberX4()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.6")]
        public void OperatorXReturnsTonumberX5()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.6/S11.4.6_A3_T5.js", false);
        }


    }
}
