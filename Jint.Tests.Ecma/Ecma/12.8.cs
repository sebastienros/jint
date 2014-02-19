using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.8")]
        public void TheBreakStatementABreakStatementWithoutAnIdentifierMayHaveALineterminatorBeforeTheSemiColon()
        {
			RunTest(@"TestCases/ch12/12.8/12.8-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithoutAnIterationstatementLeadsToSyntaxError()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithoutAnIterationstatementLeadsToSyntaxError2()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithoutAnIterationstatementLeadsToSyntaxError3()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithoutAnIterationstatementLeadsToSyntaxError4()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A1_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void SinceLineterminatorBetweenBreakAndIdentifierIsNotAllowedBreakIsEvaluatedWithoutLabel()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void WhenBreakIsEvaluatedBreakEmptyEmptyIsReturned()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void WhenBreakIdentifierIsEvaluatedBreakEmptyIdentifierIsReturned()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void WhenBreakIdentifierIsEvaluatedBreakEmptyIdentifierIsReturned2()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void WhenBreakIdentifierIsEvaluatedBreakEmptyIdentifierIsReturned3()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void IdentifierMustBeLabelInTheLabelSetOfAnEnclosingButNotCrossingFunctionBoundariesIterationstatement()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A5_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void IdentifierMustBeLabelInTheLabelSetOfAnEnclosingButNotCrossingFunctionBoundariesIterationstatement2()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A5_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void IdentifierMustBeLabelInTheLabelSetOfAnEnclosingButNotCrossingFunctionBoundariesIterationstatement3()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A5_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithinAFunctionCallThatIsNestedInAIterationstatementYieldsSyntaxerror()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A6.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithinEvalStatementThatIsNestedInAnIterationstatementYieldsSyntaxerror()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A7.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithinTryCatchBlockYieldsSyntaxerror()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A8_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void AppearingOfBreakWithinTryCatchBlockYieldsSyntaxerror2()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A8_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void UsingBreakWithinTryCatchStatementThatIsNestedInALoopIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.8")]
        public void UsingBreakWithinTryCatchStatementThatIsNestedInALoopIsAllowed2()
        {
			RunTest(@"TestCases/ch12/12.8/S12.8_A9_T2.js", false);
        }


    }
}
