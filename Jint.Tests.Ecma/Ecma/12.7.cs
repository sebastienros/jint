using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.7")]
        public void TheContinueStatementAContinueStatementWithoutAnIdentifierMayHaveALineterminatorBeforeTheSemiColon()
        {
			RunTest(@"TestCases/ch12/12.7/12.7-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithoutAnIterationstatementLeadsToSyntaxError()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithoutAnIterationstatementLeadsToSyntaxError2()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithoutAnIterationstatementLeadsToSyntaxError3()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithoutAnIterationstatementLeadsToSyntaxError4()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A1_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void SinceLineterminatorBetweenContinueAndIdentifierIsNotAllowedContinueIsEvaluatedWithoutLabel()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void WhenContinueIdentifierIsEvaluatedIdentifierMustBeLabelInTheLabelSetOfAnEnclosingButNotCrossingFunctionBoundariesIterationstatement()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A5_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void WhenContinueIdentifierIsEvaluatedIdentifierMustBeLabelInTheLabelSetOfAnEnclosingButNotCrossingFunctionBoundariesIterationstatement2()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A5_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void WhenContinueIdentifierIsEvaluatedIdentifierMustBeLabelInTheLabelSetOfAnEnclosingButNotCrossingFunctionBoundariesIterationstatement3()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A5_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithinAFunctionCallThatIsWithinAnIterationstatementYieldsSyntaxerror()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A6.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithinEvalStatementThatIsWithinAnIterationstatementYieldsSyntaxerror()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A7.js", false);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithinATryCatchBlockYieldsSyntaxerror()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A8_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void AppearingOfContinueWithinATryCatchBlockYieldsSyntaxerror2()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A8_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void ContinueInsideOfTryCatchNestedInALoopIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.7")]
        public void ContinueInsideOfTryCatchNestedInALoopIsAllowed2()
        {
			RunTest(@"TestCases/ch12/12.7/S12.7_A9_T2.js", false);
        }


    }
}
