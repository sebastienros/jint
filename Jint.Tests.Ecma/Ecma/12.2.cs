using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.2")]
        public void VariablesAreCreatedWhenTheProgramIsEnteredVariablesAreInitialisedToUndefinedWhenCreatedAVariableWithAnInitialiserIsAssignedTheValueOfItsAssignmentexpressionWhenTheVariablestatementIsExecutedNotWhenTheVariableIsCreated()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void VarStatementWithinForStatementIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A10.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void WhenUsingPropertyAttributesReadonlyIsNotUsed()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A11.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void VariabledeclarationWithinDoWhileLoopIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A12.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void VariablesAreDefinedWithGlobalScopeThatIsTheyAreCreatedAsMembersOfTheGlobalObjectAsDescribedIn1013UsingPropertyAttributesDontdelete()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void FunctiondeclarationProducesANewScope()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void UnicodeCharactersInVariableIdentifierAreAllowed()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A4.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void VariabledeclarationWithinEvalStatementIsInitializedAsTheProgramReachesTheEvalStatement()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A5.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void VariabledeclarationWithinTryCatchStatementIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void VariabledeclarationWithinTryCatchStatementIsAllowed2()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void VariabledeclarationWithinForStatementIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A7.js", false);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized2()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized3()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized4()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized5()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T5.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized6()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T6.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized7()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T7.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void OnlyAssignmentexpressionIsAdmittedWhenVariableIsInitialized8()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A8_T8.js", true);
        }

        [Fact]
        [Trait("Category", "12.2")]
        public void WhenUsingPropertyAttributesDontenumIsNotUsed()
        {
			RunTest(@"TestCases/ch12/12.2/S12.2_A9.js", false);
        }


    }
}
