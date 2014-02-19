using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.1.1")]
        public void ProgramFunctionsAreDefinedInSourceTextByAFunctiondeclarationOrCreatedDynamicallyEitherByUsingAFunctionexpressionOrByUsingTheBuiltInFunctionObjectAsAConstructor()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void ProgramFunctionsAreDefinedInSourceTextByAFunctiondeclarationOrCreatedDynamicallyEitherByUsingAFunctionexpressionOrByUsingTheBuiltInFunctionObjectAsAConstructor2()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void ProgramFunctionsAreDefinedInSourceTextByAFunctiondeclarationOrCreatedDynamicallyEitherByUsingAFunctionexpressionOrByUsingTheBuiltInFunctionObjectAsAConstructor3()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void ThereAreTwoTypesOfFunctionObjectsInternalFunctionsAreBuiltInObjectsOfTheLanguageSuchAsParseintAndMathExp()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichContainsTwoSpaceBetweenUseAndStrict()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictInWhichAllCharactersAreUppercase()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeEvalCodeIsStrictCodeWithAUseStrictDirectiveAtTheBeginningOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeEvalCodeIsStrictEvalCodeWithAUseStrictDirectiveInTheMiddleOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeEvalCodeIsStrictEvalCodeWithAUseStrictDirectiveAtTheEndOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeTheCallToEvalFunctionIsContainedInAStrictModeBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeThatIsPartOfAFunctiondeclarationIsStrictFunctionCodeIfFunctiondeclarationIsContainedInUseStrict()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeThatIsPartOfAFunctionexpressionIsStrictFunctionCodeIfFunctionexpressionIsContainedInUseStrict()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeThatIsPartOfAAccessorPropertyassignmentIsInStrictModeIfAccessorPropertyassignmentIsContainedInUseStrictGetter()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeThatIsPartOfAAccessorPropertyassignmentIsInStrictModeIfAccessorPropertyassignmentIsContainedInUseStrictSetter()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAFunctiondeclarationContainsUseStrictDirectiveWhichAppearsAtTheStartOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichLostTheLastCharacter()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAFunctiondeclarationContainsUseStrictDirectiveWhichAppearsInTheMiddleOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAFunctiondeclarationContainsUseStrictDirectiveWhichAppearsAtTheEndOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAFunctionexpressionContainsUseStrictDirectiveWhichAppearsAtTheStartOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAFunctionexpressionContainsUseStrictDirectiveWhichAppearsInTheMiddleOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAFunctionexpressionContainsUseStrictDirectiveWhichAppearsAtTheEndOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAccessorPropertyassignmentContainsUseStrictDirectiveWhichAppearsAtTheStartOfTheBlockGetter()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAccessorPropertyassignmentContainsUseStrictDirectiveWhichAppearsAtTheStartOfTheBlockSetter()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAccessorPropertyassignmentContainsUseStrictDirectiveWhichAppearsInTheMiddleOfTheBlockGetter()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfAccessorPropertyassignmentContainsUseStrictDirectiveWhichAppearsAtTheEndOfTheBlockSetter()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeTheBuiltInFunctionConstructorIsContainedInUseStrictCode()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichLostTheLastCharacter2()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-2gs.js", true);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichTheFirstCharacterIsSpace()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfBuiltInFunctionConstructorContainsUseStrictDirectiveWhichAppearsAtTheStartOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfBuiltInFunctionConstructorContainsUseStrictDirectiveWhichAppearsInTheMiddleOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeFunctionCodeOfBuiltInFunctionConstructorContainsUseStrictDirectiveWhichAppearsAtTheEndOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-32-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichTheLastCharacterIsSpace()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichAppearsAtTheBeginningOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichAppearsAtTheStartOfTheCode()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-5gs.js", true);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichAppearsInTheMiddleOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichAppearsAtTheEndOfTheBlock()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichAppearsTwiceInTheDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictWhichAppearsTwiceInTheCode()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-8gs.js", true);
        }

        [Fact]
        [Trait("Category", "10.1.1")]
        public void StrictModeUseStrictDirectivePrologueIsUseStrictInWhichTheFirstCharacterIsUppercase()
        {
			RunTest(@"TestCases/ch10/10.1/10.1.1/10.1.1-9-s.js", false);
        }


    }
}
