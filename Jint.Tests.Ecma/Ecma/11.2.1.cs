using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.2.1")]
        public void WhiteSpaceAndLineTerminatorBetweenMemberexpressionOrCallexpressionAndAndBetweenAndIdentifierAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void WhiteSpaceAndLineTerminatorBetweenAndMemberexpressionOrCallexpressionAndBetweenIdentifierAndAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void MemberexpressionAndCallexpressionUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void MemberexpressionCallsToobjectMemberexpressionAndTostringExpressionCallexpressionCallsToobjectCallexpressionAndTostringExpression()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void MemberexpressionCallsToobjectMemberexpressionAndTostringExpressionCallexpressionCallsToobjectCallexpressionAndTostringExpression2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void MemberexpressionCallsToobjectMemberexpressionAndTostringExpressionCallexpressionCallsToobjectCallexpressionAndTostringExpression3()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void MemberexpressionCallsToobjectMemberexpressionAndTostringExpressionCallexpressionCallsToobjectCallexpressionAndTostringExpression4()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void MemberexpressionCallsToobjectMemberexpressionAndTostringExpressionCallexpressionCallsToobjectCallexpressionAndTostringExpression5()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties3()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties4()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties5()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties6()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties7()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties8()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T8.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.1")]
        public void CheckTypeOfVariousProperties9()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.1/S11.2.1_A4_T9.js", false);
        }


    }
}
