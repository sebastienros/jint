using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_6_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.6.1")]
        public void WhenTheProductionDoStatementWhileExpressionIsEvaluatedStatementIsEvaluatedFirst()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void FunctionexpressionWithinADoWhileStatementIsAllowedButNoFunctionWithTheGivenNameWillAppearInTheGlobalContext()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A10.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void BlockInADoWhileExpressionIsEvaluatedToTrue()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A11.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void AnyStatementWithinDoWhileConstructionMustBeACompound()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A12.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void FunctionexpressionWithinADoWhileExpressionIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A14_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void FunctionexpressionWithinADoWhileExpressionIsAllowed2()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A14_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void BlockWithinADoWhileExpressionIsNotAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A15.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void WhileEvaluatingDoStatementWhileExpressionStatementIsEvaluatedFirstAndOnlyAfterItIsDoneExpressionIsChecked()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void WhenTheProductionDoStatementWhileExpressionIsEvaluatedThenNormalVEmptyIsReturned()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void BreakWithinADoWhileStatementIsAllowedAndPerformedAsDescribedIn128()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void BreakWithinADoWhileStatementIsAllowedAndPerformedAsDescribedIn1282()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void BreakWithinADoWhileStatementIsAllowedAndPerformedAsDescribedIn1283()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void BreakWithinADoWhileStatementIsAllowedAndPerformedAsDescribedIn1284()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void BreakWithinADoWhileStatementIsAllowedAndPerformedAsDescribedIn1285()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void AfterDoWhileIsBrokenNormalVEmptyIsReturned()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A5.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void ExpressionInDoWhileIterationstatementIsBracketedWithBraces()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A6_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void ExpressionInDoWhileIterationstatementIsBracketedWithBraces2()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A6_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void ExpressionInDoWhileIterationstatementIsBracketedWithBraces3()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A6_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void ExpressionInDoWhileIterationstatementIsBracketedWithBraces4()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A6_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void ExpressionInDoWhileIterationstatementIsBracketedWithBraces5()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A6_T5.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void ExpressionInDoWhileIterationstatementIsBracketedWithBraces6()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A6_T6.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void TheDoWhileStatementIsEvalutedAccordingTo1261AndReturnsNormalVEmpty()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A7.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void ContinueStatementWithinADoWhileStatementIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A8.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.1")]
        public void DoWhileStatementIsEvaluatedWithoutSyntaxChecks()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.1/S12.6.1_A9.js", false);
        }


    }
}
