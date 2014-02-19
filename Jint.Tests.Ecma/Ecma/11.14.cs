using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.14")]
        public void WhiteSpaceAndLineTerminatorBetweenExpressionAndOrBetweenAndAssignmentexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.14/S11.14_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.14")]
        public void OperatorUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.14/S11.14_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.14")]
        public void OperatorUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.14/S11.14_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.14")]
        public void OperatorUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.14/S11.14_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.14")]
        public void CommaOperatorEvaluatesAllExpressionsAndReturnsTheLastOfThem()
        {
			RunTest(@"TestCases/ch11/11.14/S11.14_A3.js", false);
        }


    }
}
