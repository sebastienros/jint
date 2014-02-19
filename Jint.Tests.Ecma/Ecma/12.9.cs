using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.9")]
        public void TheReturnStatementAReturnStatementWithoutAnExpressionMayHaveALineterminatorBeforeTheSemiColon()
        {
			RunTest(@"TestCases/ch12/12.9/12.9-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError2()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T10.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError3()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError4()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError5()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError6()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T5.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError7()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T6.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError8()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T7.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError9()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T8.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void AppearingOfReturnWithoutAFunctionBodyLeadsToSyntaxError10()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A1_T9.js", true);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void LineterminatorBetweenReturnAndIdentifierOptYieldsReturnWithoutIdentifierOpt()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void IfExpressionIsOmittedTheReturnValueIsUndefined()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void TheProductionReturnstatementReturnExpressionIsEvaluatedAsIEvaluateExpressionIiCallGetvalueResult2IiiReturnReturnResult3Empty()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A4.js", false);
        }

        [Fact]
        [Trait("Category", "12.9")]
        public void CodeAfterReturnstatementIsNotEvaluated()
        {
			RunTest(@"TestCases/ch12/12.9/S12.9_A5.js", false);
        }


    }
}
