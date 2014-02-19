using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_1_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.1.6")]
        public void WhiteSpaceAndLineTerminatorInsideGroupingOperatorAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.6")]
        public void ThisOperatorDoesnTUseGetvalueTheOperatorsDeleteAndTypeofCanBeAppliedToParenthesisedExpressions()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.6")]
        public void ThisOperatorOnlyEvaluatesExpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.6")]
        public void ThisOperatorOnlyEvaluatesExpression2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.6")]
        public void ThisOperatorOnlyEvaluatesExpression3()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.6")]
        public void ThisOperatorOnlyEvaluatesExpression4()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.6")]
        public void ThisOperatorOnlyEvaluatesExpression5()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.6")]
        public void ThisOperatorOnlyEvaluatesExpression6()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.6/S11.1.6_A3_T6.js", false);
        }


    }
}
