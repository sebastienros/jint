using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.5")]
        public void NullUndefinedFalseEmptyStringNanInExpressionIsEvaluatedToFalse()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void NullUndefinedFalseEmptyStringNanInExpressionIsEvaluatedToFalse2()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void TrueNonEmptyStringAndOthersInExpressionIsEvaluatedToTrueWhenUsingOperatorNew()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void TrueNonEmptyStringAndOthersInExpressionIsEvaluatedToTrueWhenUsingOperatorNew2()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void FunctionExpessionInsideTheIfExpressionIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A10_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void FunctionExpessionInsideTheIfExpressionIsAllowed2()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A10_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void WithinTheIfExpressionIsNotAllowed()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A11.js", true);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void EmbeddedIfElseConstructionsAreAllowed()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void EmbeddedIfElseConstructionsAreAllowed2()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void EmbeddedIfElseConstructionsAreAllowed3()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A12_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void EmbeddedIfElseConstructionsAreAllowed4()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A12_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void TrueNonEmptyStringInExpressionIsEvaluatedToTrue()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void TrueNonEmptyStringInExpressionIsEvaluatedToTrue2()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void InTheIfStatementEvalInExpressionIsAdmitted()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A2.js", true);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void WhenTheProductionIfstatementIfExpressionStatementElseStatementIsEvaluatedExpressionIsEvaluatedFirst()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void WhenTheProductionIfstatementIfExpressionStatementElseStatementIsEvaluatedStatementSIsAreEvaluatedSecond()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A4.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void FunctiondeclarationInsideTheIfExpressionIsEvaluatedAsTrueAndFunctionWillNotBeDeclarated()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A5.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void InTheIfStatementExpressionMustBeEnclosedInBraces()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A6_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void InTheIfStatementExpressionMustBeEnclosedInBraces2()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A6_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void InTheIfStatementEmptyStatementIsAllowedAndIsEvaluatedToUndefined()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A7.js", false);
        }

        [Fact]
        [Trait("Category", "12.5")]
        public void InTheIfStatementEmptyExpressionIsNotAllowed()
        {
			RunTest(@"TestCases/ch12/12.5/S12.5_A8.js", true);
        }


    }
}
