using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.4.4")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.4-4.a-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void StrictModeSyntaxerrorIsThrownForEval()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/11.4.4-2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void StrictModeSyntaxerrorIsThrownForArguments()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/11.4.4-2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void StrictModeSyntaxerrorIsNotThrownForArguments()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/11.4.4-2-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void WhiteSpaceAndLineTerminatorBetweenAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXUsesGetvalueAndPutvalue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXUsesGetvalueAndPutvalue2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXUsesGetvalueAndPutvalue3()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A2.1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsXTonumberX1()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsXTonumberX12()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsXTonumberX13()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsXTonumberX14()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsXTonumberX15()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsTonumberX1()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsTonumberX12()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsTonumberX13()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsTonumberX14()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.4")]
        public void OperatorXReturnsTonumberX15()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.4/S11.4.4_A4_T5.js", false);
        }


    }
}
