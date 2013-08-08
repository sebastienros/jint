using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "14")]
        public void FunctionexpressionMustBeLocaledInAReacheableFragmentOfTheProgram()
        {
			RunTest(@"TestCases/ch14/14.0/S14_A1.js", false);
        }

        [Fact]
        [Trait("Category", "14")]
        public void FunctiondeclarationCannotBeLocaledInsideAnExpression()
        {
			RunTest(@"TestCases/ch14/14.0/S14_A2.js", false);
        }

        [Fact]
        [Trait("Category", "14")]
        public void GlobalFunctiondeclarationCannotBeDefinedWithinTheBodyOfAnotherFunctiondeclaration()
        {
			RunTest(@"TestCases/ch14/14.0/S14_A3.js", false);
        }

        [Fact]
        [Trait("Category", "14")]
        public void TheIdentiferWithinAFunctiondeclarationCanBeWrittenInBothLettersAndUnicode()
        {
			RunTest(@"TestCases/ch14/14.0/S14_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "14")]
        public void TheIdentiferWithinAFunctiondeclarationCanBeWrittenInBothLettersAndUnicode2()
        {
			RunTest(@"TestCases/ch14/14.0/S14_A5_T2.js", false);
        }


    }
}
