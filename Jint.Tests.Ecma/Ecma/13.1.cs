using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_13_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "13.1")]
        public void DuplicateIdentifierAllowedInNonStrictFunctionDeclarationParameterList()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void DuplicateIdentifierAllowedInNonStrictFunctionExpressionParameterList()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfEvalOccursAsTheFunctionNameOfAFunctiondeclarationInStrictMode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfEvalOccursAsTheIdentifierOfAFunctionexpressionInStrictMode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfArgumentsOccursAsTheFunctionNameOfAFunctiondeclarationInStrictMode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfArgumentsOccursAsTheIdentifierOfAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-13gs.js", true);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfArgumentsOccursAsTheIdentifierOfAFunctionexpressionInStrictMode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression2()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression3()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression4()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression5()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression6()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearsWithinAFormalparameterlistOfAStrictModeFunctiondeclaration()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void EvalAllowedAsFormalParameterNameOfANonStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void EvalAllowedAsFormalParameterNameOfANonStrictFunctionExpression()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void ArgumentsAllowedAsFormalParameterNameOfANonStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void ArgumentsAllowedAsFormalParameterNameOfANonStrictFunctionExpression()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression7()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression8()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression9()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression10()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression2()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression3()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression4()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression5()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression6()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression7()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression8()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void EvalAllowedAsFunctionIdentifierInNonStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void EvalAllowedAsFunctionIdentifierInNonStrictFunctionExpression()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void ArgumentsAllowedAsFunctionIdentifierInNonStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void ArgumentsAllowedAsFunctionIdentifierInNonStrictFunctionExpression()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression11()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression9()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression10()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression11()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-32-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression12()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-33-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression13()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-34-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfEvalOccursAsTheFunctionNameOfAFunctiondeclarationInStrictEvalCode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-35-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfEvalOccursAsTheFunctionNameOfAFunctiondeclarationWhoseFunctionbodyIsInStrictMode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-36-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfEvalOccursAsTheIdentifierOfAFunctionexpressionInStrictEvalCode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-37-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfEvalOccursAsTheIdentifierOfAFunctionexpressionWhoseFunctionbodyIsContainedInStrictCode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-38-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfArgumentsOccursAsTheFunctionNameOfAFunctiondeclarationInStrictEvalCode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-39-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfTheIdentifierEvalOrTheIdentifierArgumentsOccursWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression12()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfArgumentsOccursAsTheIdentifierOfAFunctiondeclarationWhoseFunctionbodyIsContainedInStrictCode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-40-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfArgumentsOccursAsTheIdentifierOfAFunctionexpressionInStrictEvalCode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-41-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictmodeSyntaxerrorIsThrownIfArgumentsOccursAsTheIdentifierOfAFunctionexpressionWhoseFunctionbodyIsContainedInStrictCode()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-42-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearsWithinAFormalparameterlistOfAStrictModeFunctionexpression()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-4gs.js", true);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression14()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictModeSyntaxerrorIsThrownIfAFunctiondeclarationHasTwoIdenticalParameters()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-5gs.js", true);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression15()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression16()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression17()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void StrictModeSyntaxerrorIsThrownIfAFunctionexpressionHasTwoIdenticalParameters()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-8gs.js", true);
        }

        [Fact]
        [Trait("Category", "13.1")]
        public void Refer131ItIsASyntaxerrorIfAnyIdentifierValueOccursMoreThanOnceWithinAFormalparameterlistOfAStrictModeFunctiondeclarationOrFunctionexpression18()
        {
			RunTest(@"TestCases/ch13/13.1/13.1-9-s.js", false);
        }


    }
}
