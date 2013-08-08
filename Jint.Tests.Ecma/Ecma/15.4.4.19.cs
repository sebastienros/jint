using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_19 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthMustBe1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectWhenLengthIsAnOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectLengthIsAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectWhenLengthIsAnOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToTheArrayLikeObjectWhenLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToTheArrayLikeObjectWhenLengthIsInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToTheArrayLikeObjectThatLengthPropertyDoesnTExist()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapWhenLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapWhenLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapWhenLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectLengthIsAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectLengthIsAnOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAppliedToArrayLikeObjectWhenLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsStringThatIsAbleToConvertToNumberPrimitiveValueIsADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapWhenLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapOnAnArrayLikeObjectIfLengthIs1LengthOverriddenToTrueTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAStringContainingAPositiveNumber2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapLengthIsAStringContainingANegativeNumber2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsTypeerrorIfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapTheExceptionIsNotThrownIfExceptionWasThrownByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapTheExceptionIsNotThrownIfExceptionWasThrownByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallingWithNoCallbackfnIsTheSameAsPassingUndefinedForCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsReferenceerrorIfCallbackfnIsUnreferenced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsTypeerrorIfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsTypeerrorIfCallbackfnIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsTypeerrorIfCallbackfnIsNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsTypeerrorIfCallbackfnIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThrowsTypeerrorIfCallbackfnIsObjectWithoutCallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisargNotPassedToStrictCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisargNotPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapArrayObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapStringObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapBooleanObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapNumberObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapTheMathObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDateObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapRegexpObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapTheJsonObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapErrorObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapTheArgumentsObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisargIsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapTheGlobalObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapBooleanPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapNumberPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapStringPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisargIsArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisargIsObjectFromObjectTemplatePrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisargIsObjectFromObjectTemplate()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisargIsFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapBuiltInFunctionsCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapFunctionObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapArrayIsarrayReturnsTrueWhenInputArgumentIsTheOurputArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapTheReturnedArrayIsInstanceofArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDoesnTConsiderNewElementsAddedToArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapConsidersNewValueOfElementsInArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDoesnTVisitDeletedElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDoesnTVisitDeletedElementsWhenArrayLengthIsDecreased()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDoesnTConsiderNewlyAddedElementsInSparseArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapVisitsDeletedElementInArrayAfterTheCallWhenSameIndexIsAlsoPresentInPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapSuccessfulToDeleteTheObjectInCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapNoObservableEffectsOccurIfLengthIs0OnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapModificationsToLengthDonTChangeNumberOfIterationsOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnNotCalledForIndexesNeverBeenAssignedValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDecreasingLengthOfArrayCausesIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisObjectIsTheGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementChangedByGetterOnPreviousIterationsIsObservedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementChangedByGetterOnPreviousIterationsIsObservedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapUnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapUnhandledExceptionsHappenedInGetterTerminateIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnCalledWithCorrectParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnIsCalledWith1FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnIsCalledWith2FormalParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnIsCalledWith3FormalParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnThatUsesArgumentsObjectToGetParameterValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisObjectWhenTIsNotAnObjectTIsABooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisObjectWhenTIsNotAnObjectTIsANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapThisObjectWhenTIsNotAnObjectTIsAStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapNonIndexedPropertiesAreNotCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnTakes3Arguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnCalledWithCorrectParametersThisargIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnCalledWithCorrectParametersKvalueIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnCalledWithCorrectParametersTheIndexKIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnCalledWithCorrectParametersThisObjectOIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapKValuesArePassedInAcendingNumericOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapKValuesAreAccessedDuringEachIterationAndNotPriorToStartingTheLoop()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapArgumentsToCallbackfnAreSelfConsistent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapUnhandledExceptionsHappenedInCallbackfnTerminateIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapElementChangedByCallbackfnOnPreviousIterationsIsObserved()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapCallbackfnWith0FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapGetownpropertydescriptorAllTrueOfReturnedArrayElement()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-iii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfReturnedArrayElementEqualsToMappedvalue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-iii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfReturnedArrayElementCanBeOverwritten()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-iii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfReturnedArrayElementCanBeEnumerated()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-iii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapValueOfReturnedArrayElementCanBeChangedOrDeleted()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-iii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDoesnTMutateTheArrayOnWhichItIsCalledOn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapEmptyArrayToBeReturnedIfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenWith()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapIfThereAreNoSideEffectsOfTheFunctionsOIsUnmodified()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapReturnsNewArrayWithSameNumberOfElementsAndValuesTheResultOfCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapSubclassedArrayWhenLengthIsReduced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapDoesnTVisitExpandos()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapEmptyArrayToBeReturnedIfLengthIs0EmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapEmptyArrayToBeReturnedIfLengthIs0SubclassedArrayLengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapEmptyArrayToBeReturnedIfLengthIs0SubclassedArrayLengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapEmptyArrayToBeReturnedIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.19")]
        public void ArrayPrototypeMapEmptyArrayToBeReturnedIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-9.js", false);
        }


    }
}
