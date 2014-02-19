using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_13 : EcmaTest
    {
        [Fact]
        [Trait("Category", "13")]
        public void XFunctionYStatementDoesNotStoreAReferenceToTheNewFunctionInTheVaraibleYIdentifier()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionIsAData()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A10.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void SinceArgumentsPropertyHasAttributeDontdeleteOnlyItsElementsCanBeDeleted()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void SinceArgumentsPropertyHasAttributeDontdeleteOnlyItsElementsCanBeDeleted2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A11_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void SinceArgumentsPropertyHasAttributeDontdeleteOnlyItsElementsCanBeDeleted3()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A11_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void SinceArgumentsPropertyHasAttributeDontdeleteOnlyItsElementsCanBeDeleted4()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A11_T4.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionDeclarationsInGlobalOrFunctionScopeAreDontdelete()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionDeclarationsInGlobalOrFunctionScopeAreDontdelete2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void DeletingArgumentsILeadsToBreakingTheConnectionToLocalReference()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A13_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void DeletingArgumentsILeadsToBreakingTheConnectionToLocalReference2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A13_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void DeletingArgumentsILeadsToBreakingTheConnectionToLocalReference3()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A13_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void UnicodeSymbolsInFunctionNameAreAllowed()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A14.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ArgumentsVariableOverridesActivationobjectArguments()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A15_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ArgumentsVariableOverridesActivationobjectArguments2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A15_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ArgumentsVariableOverridesActivationobjectArguments3()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A15_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ArgumentsVariableOverridesActivationobjectArguments4()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A15_T4.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ArgumentsVariableOverridesActivationobjectArguments5()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A15_T5.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void AnySeparatorsAreAdmittedBetweenDeclarationChunks()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A16.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionCallCannotAppearInTheProgramBeforeTheFunctionexpressionAppears()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A17_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionCallCannotAppearInTheProgramBeforeTheFunctionexpressionAppears2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A17_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ClosuresAreAdmitted()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A18.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void VarDoesNotOverrideFunctionDeclaration()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A19_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void VarDoesNotOverrideFunctionDeclaration2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A19_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionMustBeEvaluatedInsideTheExpression()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionMustBeEvaluatedInsideTheExpression2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionMustBeEvaluatedInsideTheExpression3()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheIdentifierInAFunctionexpressionCanBeReferencedFromInsideTheFunctionexpressionSFunctionbodyToAllowTheFunctionCallingItselfRecursively()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheIdentifierInAFunctionexpressionCanBeReferencedFromInsideTheFunctionexpressionSFunctionbodyToAllowTheFunctionCallingItselfRecursively2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheIdentifierInAFunctionexpressionCanBeReferencedFromInsideTheFunctionexpressionSFunctionbodyToAllowTheFunctionCallingItselfRecursively3()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheProductionFunctiondeclarationFunctionIdentifierFormalparameterlistOptFunctionbodyIsProcessedByFunctionDeclarations()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheProductionFunctiondeclarationFunctionIdentifierFormalparameterlistOptFunctionbodyIsProcessedByFunctionDeclarations2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheProductionFunctiondeclarationFunctionIdentifierFormalparameterlistOptFunctionbodyIsProcessedByFunctionDeclarations3()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheProductionFunctiondeclarationFunctionIdentifierFormalparameterlistOptFunctionbodyIsProcessedByFunctionDeclarations4()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctiondeclarationCanBeOverridedByOtherFunctiondeclarationWithTheSameIdentifier()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctiondeclarationCanBeOverridedByOtherFunctiondeclarationWithTheSameIdentifier2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheFunctionbodyMustBeSourceelements()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheFunctionbodyMustBeSourceelements2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void TheFunctionbodyMustBeSourceelements3()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A7_T3.js", true);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ArgumentsPropertyOfActivationObjectContainsRealParamsToBePassed()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void ArgumentsPropertyOfActivationObjectContainsRealParamsToBePassed2()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13")]
        public void FunctionCanBePassedAsArgument()
        {
			RunTest(@"TestCases/ch13/13.0/S13_A9.js", false);
        }


    }
}
