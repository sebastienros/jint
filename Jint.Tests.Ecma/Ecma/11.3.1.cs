using Xunit;

namespace Jint.Tests.Ecma
{
    [Trait("Category","Pass")]
    public class Test_11_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.3.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsAPostfixexpressionArguments()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/11.3.1-2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void StrictModeSyntaxerrorIsThrowIfTheIdentifierArgumentsAppearAsAPostfixexpressionArguments()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/11.3.1-2-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsAPostfixexpressionEval()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/11.3.1-2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void StrictModeSyntaxerrorIsNotThrownIfTheIdentifierArgumentsAppearsAsAPostfixexpressionArguments()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/11.3.1-2-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void LineTerminatorBetweenLefthandsideexpressionAndIsNotAllowed()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A1.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void LineTerminatorBetweenLefthandsideexpressionAndIsNotAllowed2()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A1.1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void LineTerminatorBetweenLefthandsideexpressionAndIsNotAllowed3()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A1.1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void LineTerminatorBetweenLefthandsideexpressionAndIsNotAllowed4()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A1.1_T4.js", true);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void WhiteSpaceBetweenLefthandsideexpressionAndAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXUsesGetvalueAndPutvalue()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXUsesGetvalueAndPutvalue2()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXUsesGetvalueAndPutvalue3()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A2.1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsXTonumberX1()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsXTonumberX12()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsXTonumberX13()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsXTonumberX14()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsXTonumberX15()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsTonumberX()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsTonumberX2()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsTonumberX3()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsTonumberX4()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.3.1")]
        public void OperatorXReturnsTonumberX5()
        {
			RunTest(@"TestCases/ch11/11.3/11.3.1/S11.3.1_A4_T5.js", false);
        }


    }
}
