using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsIRemainsSameAfterChangingActualParametersInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-10-c-ii-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsIChangeWithActualParameters()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-10-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsIDoesnTMapToActualParametersInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-10-c-ii-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsIMapToActualParameter()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-10-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsObjectHasIndexProperty0AsItsOwnPropertyItShouldeBeWritableEnumerableConfigurableAndDoesNotInvokeTheSetterDefinedOnObjectPrototype0Step11B()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-11-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void AccessingCalleePropertyOfArgumentsObjectIsAllowed()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-12-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsCalleeHasCorrectAttributes()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-12-2.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void AccessingCallerPropertyOfArgumentsObjectIsAllowed()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void InNonStrictModeArgumentsObjectShouldHaveItsOwnCalleePropertyDefinedStep13A()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ADirectCallToArgumentsCalleeCallerShouldWork()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void AnIndirectCallToArgumentsCalleeCallerShouldWork()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void AccessingCallerPropertyOfArgumentsObjectThrowsTypeerrorInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-b-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsCallerExistsInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-b-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsCallerIsNonConfigurableInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-b-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void AccessingCalleePropertyOfArgumentsObjectThrowsTypeerrorInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-c-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsCalleeIsExistsInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-c-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsCalleeIsNonConfigurableInStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-13-c-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void StrictModeCalleeExistsAndCallerExistsUnderStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-14-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void StrictModeEnumerableAttributeValueInCallerIsFalseUnderStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-14-b-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void StrictModeTypeerrorIsThrownWhenAccessingTheSetAttributeInCallerUnderStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-14-b-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void StrictModeEnumerableAttributeValueInCalleeIsFalseUnderStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-14-c-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void StrictModeTypeerrorIsThrownWhenAccessingTheSetAttributeInCalleeUnderStrictMode()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-14-c-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void StrictModeArgumentsCalleeCannotBeAccessedInAStrictFunctionButDoesNotThrowAnEarlyError()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-1gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void StrictModeArgumentsCalleeCannotBeAccessedInAStrictFunction()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-2gs.js", true);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void PrototypePropertyOfArgumentsIsSetToObjectPrototypeObject()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void LengthPropertyOfArgumentsObjectExists()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void LengthPropertyOfArgumentsObjectHasCorrectAttributes()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void LengthPropertyOfArgumentsObjectFor0ArgumentFunctionExists()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-6-3.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void LengthPropertyOfArgumentsObjectFor0ArgumentFunctionCallIs0EvenWithFormalParameters()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-6-4.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void ArgumentsObjectHasLengthAsItsOwnPropertyAndDoesNotInvokeTheSetterDefinedOnObjectPrototypeLengthStep7()
        {
			RunTest(@"TestCases/ch10/10.6/10.6-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void WhenControlEntersAnExecutionContextForFunctionCodeAnArgumentsObjectIsCreatedAndInitialised()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void TheValueOfTheInternalPrototypePropertyOfTheCreatedArgumentsObjectIsTheOriginalObjectPrototypeObjectTheOneThatIsTheInitialValueOfObjectPrototype()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A2.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameCalleeWithPropertyAttributesDontenumAndNoOthers()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameCalleeWithPropertyAttributesDontenumAndNoOthers2()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameCalleeWithPropertyAttributesDontenumAndNoOthers3()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameCalleeWithPropertyAttributesDontenumAndNoOthers4()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void TheInitialValueOfTheCreatedPropertyCalleeIsTheFunctionObjectBeingExecuted()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A4.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameLengthWithPropertyAttributesDontenumAndNoOthers()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameLengthWithPropertyAttributesDontenumAndNoOthers2()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameLengthWithPropertyAttributesDontenumAndNoOthers3()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void APropertyIsCreatedWithNameLengthWithPropertyAttributesDontenumAndNoOthers4()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void TheInitialValueOfTheCreatedPropertyLengthIsTheNumberOfActualParameterValuesSuppliedByTheCaller()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A6.js", false);
        }

        [Fact]
        [Trait("Category", "10.6")]
        public void GetArgumentsOfFunction()
        {
			RunTest(@"TestCases/ch10/10.6/S10.6_A7.js", false);
        }


    }
}
