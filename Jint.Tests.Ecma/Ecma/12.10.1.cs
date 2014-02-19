using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_10_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorStrictFunction()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorEvalWhereTheContainerFunctionIsStrict()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void StrictModeSyntaxerrorIsThrownWhenUsingWithstatementInStrictModeCode()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void StrictModeSyntaxerrorIsThrownWhenUsingWithStatement()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-11gs.js", true);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorStrictEval()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void StrictModeSyntaxerrorIsnTThrownWhenWithstatementBodyIsInStrictModeCode()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void StrictModeSyntaxerrorIsThrownWhenTheGetterOfALiteralObjectUtilizesWithstatement()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void StrictModeSyntaxerrorIsThrownWhenTheRhsOfADotPropertyAssignmentUtilizesWithstatement()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void StrictModeSyntaxerrorIsThrownWhenTheRhsOfAnObjectIndexerAssignmentUtilizesWithstatement()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorNestedFunctionWhereContainerIsStrict()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorNestedStrictFunction()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorStrictFunction2()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementAllowedInNestedFunctionEvenIfItsContainerFunctionIsStrict()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorFunctionExpressionWhereTheContainerFunctionIsDirectlyEvaledFromStrictCode()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorFunctionExpressionWhereTheContainerFunctionIsStrict()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.10.1")]
        public void WithStatementInStrictModeThrowsSyntaxerrorStrictFunctionExpression()
        {
			RunTest(@"TestCases/ch12/12.10/12.10.1/12.10.1-9-s.js", false);
        }


    }
}
