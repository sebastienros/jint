using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_20 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthMustBe1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToUndefinedThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToNullThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDoesnTMutateTheArrayOnWhichItIsCalledOn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-10-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsNewArrayWithLengthEqualToNumberOfTrueReturnedByCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-10-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterSubclassedArrayWhenLengthIsReduced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-10-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDoesnTVisitExpandos()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-10-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToTheArrayLikeObjectThatLengthIsInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToTheArrayLikeObjectThatLengthPropertyDoesnTExist()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsAnOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAppliedOnAnArrayLikeObjectIfLengthIs1LengthOverriddenToTrueTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIsPositive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIsNegative()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsTypeerrorIfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterTheExceptionIsNotThrownIfExceptionWasThrownByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterTheExceptionIsNotThrownIfExceptionWasThrownByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallingWithNoCallbackfnIsTheSameAsPassingUndefinedForCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsReferenceerrorIfCallbackfnIsUnreferenced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsTypeerrorIfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsTypeerrorIfCallbackfnIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsTypeerrorIfCallbackfnIsNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsTypeerrorIfCallbackfnIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThrowsTypeerrorIfCallbackfnIsObjectWithoutCallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargNotPassedToStrictCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargIsPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterArrayObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterStringObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterBooleanObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterNumberObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterTheMathObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDateObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterRegexpObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterTheJsonObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterErrorObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterTheArgumentsObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargIsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterTheGlobalObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterBooleanPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterNumberPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterStringPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterArrayIsarrayArgReturnsTrueWhenArgIsTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterTheReturnedArrayIsInstanceofArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnArrayWhoseLengthIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargIsArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargNotPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargIsObjectFromObjectTemplatePrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargIsObjectFromObjectTemplate()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisargIsFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterBuiltInFunctionsCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterFunctionObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0EmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnsAnEmptyArrayIfLengthIs0SubclassedArrayLengthOverriddenWith()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDoesnTConsiderNewElementsAddedToArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterConsidersNewValueOfElementsInArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDoesnTVisitDeletedElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDoesnTVisitDeletedElementsWhenArrayLengthIsDecreased()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDoesnTConsiderNewlyAddedElementsInSparseArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterVisitsDeletedElementInArrayAfterTheCallWhenSameIndexIsAlsoPresentInPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterStopsCallingCallbackfnOnceTheArrayIsDeletedDuringTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterNoObservableEffectsOccurIfLenIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterModificationsToLengthDonTChangeNumberOfIterations()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnNotCalledForIndexesNeverBeenAssignedValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDecreasingLengthOfArrayCausesIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisObjectIsTheGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementChangedByGetterOnPreviousIterationsIsObservedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementChangedByGetterOnPreviousIterationsIsObservedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnCalledWithCorrectParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnIsCalledWith1FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnIsCalledWith2FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnIsCalledWith3FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnThatUsesArgumentsObjectToGetParameterValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisOfCallbackfnIsABooleanObjectWhenTIsNotAnObjectTIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisOfCallbackfnIsANumberObjectWhenTIsNotAnObjectTIsANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterThisOfCallbackfnIsAnStringObjectWhenTIsNotAnObjectTIsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterNonIndexedPropertiesAreNotCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnTakes3Arguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnCalledWithCorrectParametersThisargIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnCalledWithCorrectParametersKvalueIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnCalledWithCorrectParametersTheIndexKIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnCalledWithCorrectParametersThisObjectOIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterKValuesArePassedInAscendingNumericOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterKValuesAreAccessedDuringEachIterationAndNotPriorToStartingTheLoopOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterArgumentsToCallbackfnAreSelfConsistent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterUnhandledExceptionsHappenedInCallbackfnTerminateIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterElementChangedByCallbackfnOnPreviousIterationsIsObserved()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterCallbackfnIsCalledWith0FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfReturnedArrayElementEqualsToKvalue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfReturnedArrayElementCanBeOverwritten()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfReturnedArrayElementCanBeEnumerated()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValueOfReturnedArrayElementCanBeChangedOrDeleted()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValuesOfToArePassedInAcendingNumericOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterValuesOfToAreAccessedDuringEachIterationWhenSelectedIsConvertedToTrueAndNotPriorToStartingTheLoop()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterGetownpropertydescriptorAllTrueOfReturnedArrayElement()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberValueIsNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANonEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsAStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsADateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterFalsePreventsElementAddedToOutputArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueNewBooleanFalseOfCallbackfnIsTreatedAsTrueValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsABooleanValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsABooleanValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANunmberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.20")]
        public void ArrayPrototypeFilterReturnValueOfCallbackfnIsANumberValueIsPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-9.js", false);
        }


    }
}
