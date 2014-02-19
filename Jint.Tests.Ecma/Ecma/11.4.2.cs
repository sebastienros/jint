using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.4.2")]
        public void WhiteSpaceAndLineTerminatorBetweenVoidAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidEvaluatesUnaryexpressionAndReturnsUndefined()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidEvaluatesUnaryexpressionAndReturnsUndefined2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidEvaluatesUnaryexpressionAndReturnsUndefined3()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidEvaluatesUnaryexpressionAndReturnsUndefined4()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidEvaluatesUnaryexpressionAndReturnsUndefined5()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.2")]
        public void OperatorVoidEvaluatesUnaryexpressionAndReturnsUndefined6()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.2/S11.4.2_A4_T6.js", false);
        }


    }
}
