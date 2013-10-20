using Xunit;

namespace Jint.Tests.Ecma
{
    [Trait("Category","Pass")]
    public class Test_11_8_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.8.6")]
        public void WhiteSpaceAndLineTerminatorBetweenRelationalexpressionAndInstanceofAndBetweenInstanceofAndShiftexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OperatorInstanceofUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OperatorInstanceofUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OperatorInstanceofUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void IfShiftexpressionIsNotAnObjectThrowTypeerror()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OnlyConstructorCallWithNewKeywordMakesInstance()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OnlyConstructorCallWithNewKeywordMakesInstance2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OnlyConstructorCallWithNewKeywordMakesInstance3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void TypeerrorIsSubclassOfErrorFromInstanceofOperatorPointOfView()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void TypeerrorIsSubclassOfErrorFromInstanceofOperatorPointOfView2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OnlyFunctionObjectsImplementHasinstanceAndCanBeProperShiftexpressionForTheInstanceofOperatorConsequently()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OnlyFunctionObjectsImplementHasinstanceAndCanBeProperShiftexpressionForTheInstanceofOperatorConsequently2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OnlyFunctionObjectsImplementHasinstanceAndCanBeProperShiftexpressionForTheInstanceofOperatorConsequently3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void OnlyFunctionObjectsImplementHasinstanceAndCanBeProperShiftexpressionForTheInstanceofOperatorConsequently4()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A6_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void WhenInstanceofReturnsTrueItMeansThatGetvalueRelationalexpressionIsConstructedWithShiftexpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void WhenInstanceofReturnsTrueItMeansThatGetvalueRelationalexpressionIsConstructedWithShiftexpression2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.6")]
        public void WhenInstanceofReturnsTrueItMeansThatGetvalueRelationalexpressionIsConstructedWithShiftexpression3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.6/S11.8.6_A7_T3.js", false);
        }


    }
}
