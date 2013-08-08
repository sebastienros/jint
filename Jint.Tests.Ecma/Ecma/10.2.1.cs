using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.2.1")]
        public void IfTheCallerSuppliesFewerParameterValuesThanThereAreFormalParametersTheExtraFormalParametersHaveValueUndefined()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1")]
        public void IfTwoOrMoreFormalParametersShareTheSameNameHenceTheSamePropertyTheCorrespondingPropertyIsGivenTheValueThatWasSuppliedForTheLastParameterWithThisName()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1")]
        public void IfTheValueOfThisLastParameterWhichHasTheSameNameAsSomePreviousParametersDoWasNotSuppliedByTheCallerTheValueOfTheCorrespondingPropertyIsUndefined()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1")]
        public void FunctionDeclarationInFunctionCodeIfTheVariableObjectAlreadyHasAPropertyWithTheNameOfFunctionIdentifierReplaceItsValueAndAttributesSemanticallyThisStepMustFollowTheCreationOfFormalparameterlistProperties()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1")]
        public void FunctionDeclarationInFunctionCodeIfTheVariableObjectAlreadyHasAPropertyWithTheNameOfFunctionIdentifierReplaceItsValueAndAttributesSemanticallyThisStepMustFollowTheCreationOfFormalparameterlistProperties2()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1")]
        public void ForEachVariabledeclarationOrVariabledeclarationnoinInTheCodeCreateAPropertyOfTheVariableObjectWhoseNameIsTheIdentifierInTheVariabledeclarationOrVariabledeclarationnoinWhoseValueIsUndefinedAndWhoseAttributesAreDeterminedByTheTypeOfCode()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A5.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1")]
        public void ForEachVariabledeclarationOrVariabledeclarationnoinInTheCodeCreateAPropertyOfTheVariableObjectWhoseNameIsTheIdentifierInTheVariabledeclarationOrVariabledeclarationnoinWhoseValueIsUndefinedAndWhoseAttributesAreDeterminedByTheTypeOfCode2()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A5.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1")]
        public void IfThereIsAlreadyAPropertyOfTheVariableObjectWithTheNameOfADeclaredVariableTheValueOfThePropertyAndItsAttributesAreNotChanged()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/S10.2.1_A5.2_T1.js", false);
        }


    }
}
