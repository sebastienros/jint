using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.4")]
        public void AnExpressionstatementCanNotStartWithTheFunctionKeywordBecauseThatMightMakeItAmbiguousWithAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch12/12.4/S12.4_A1.js", true);
        }

        [Fact]
        [Trait("Category", "12.4")]
        public void TheProductionExpressionstatementLookaheadNotinFunctionExpressionIsEvaluatedAsFollows1EvaluateExpression2CallGetvalueResult13ReturnNormalResult2Empty()
        {
			RunTest(@"TestCases/ch12/12.4/S12.4_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.4")]
        public void TheProductionExpressionstatementLookaheadNotinFunctionExpressionIsEvaluatedAsFollows1EvaluateExpression2CallGetvalueResult13ReturnNormalResult2Empty2()
        {
			RunTest(@"TestCases/ch12/12.4/S12.4_A2_T2.js", false);
        }


    }
}
