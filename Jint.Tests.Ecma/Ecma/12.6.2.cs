using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_6_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.6.2")]
        public void ExpressionFromWhileIterationstatementIsEvaluatedFirstFalse0NullUndefinedAndEmptyStringsUsedAsTheExpressionAreEvaluatedToFalse()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void FunctionexpressionWithinAWhileIterationstatementIsAllowedButNoFunctionWithTheGivenNameWillAppearInTheGlobalContext()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A10.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void BlockWithinAWhileExpressionIsEvaluatedToTrue()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A11.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void FunctionexpressionWithinAWhileExpressionIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A14_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void FunctionexpressionWithinAWhileExpressionIsAllowed2()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A14_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void BlockWithinAWhileExpressionIsNotAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A15.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void WhileEvaluatingTheProductionIterationstatementWhileExpressionStatementExpressionIsEvaluatedFirst()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void WhenWhileIterationstatementIsEvaluatedNormalVEmptyIsReturned()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void BreakWithinAWhileStatementIsAllowedAndPerformedAsDescribedIn128()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void BreakWithinAWhileStatementIsAllowedAndPerformedAsDescribedIn1282()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void BreakWithinAWhileStatementIsAllowedAndPerformedAsDescribedIn1283()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void BreakWithinAWhileStatementIsAllowedAndPerformedAsDescribedIn1284()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void BreakWithinAWhileStatementIsAllowedAndPerformedAsDescribedIn1285()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void WhileUsingWhileWithinAnEvalStatementSourceBreakIsAllowedAndNormalVEmptyIsReturned()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A5.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void ExpressionInWhileIterationstatementIsBracketedWithBraces()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A6_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void ExpressionInWhileIterationstatementIsBracketedWithBraces2()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A6_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void ExpressionInWhileIterationstatementIsBracketedWithBraces3()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A6_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void ExpressionInWhileIterationstatementIsBracketedWithBraces4()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A6_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void ExpressionInWhileIterationstatementIsBracketedWithBraces5()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A6_T5.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void ExpressionInWhileIterationstatementIsBracketedWithBraces6()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A6_T6.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void TheWhileStatementIsEvalutedAccordingTo1262AndReturnsNormalVEmpty()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A7.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void ContinueStatementWithinAWhileStatementIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A8.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.2")]
        public void WhileStatementIsEvaluatedWithoutSyntaxChecks()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.2/S12.6.2_A9.js", false);
        }


    }
}
