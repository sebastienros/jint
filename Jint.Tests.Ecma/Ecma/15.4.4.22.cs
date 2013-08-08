using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_22 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightLengthMustBe1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToUndefinedThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToNullThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDoesnTMutateTheArrayOnWhichItIsCalledOn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReducesArrayInDescendingOrderOfIndices()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSubclassedArrayWithLength1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSubclassedArrayWithLengthMoreThan1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReducesArrayInDescendingOrderOfIndicesInitialvaluePresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSubclassedArrayWhenInitialvalueProvided()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSubclassedArrayWhenLengthTo1AndInitialvalueProvided()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDoesnTVisitExpandos()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToTheArrayLikeObjectThatLengthIsInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToTheArrayLikeObjectThatLengthPropertyDoesnTExist()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToAnArrayLikeObjectLengthIs0LengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIsPositive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIsNegative()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheExceptionIsNotThrownIfExceptionWasThrownByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheExceptionIsNotThrownIfExceptionWasThrownByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallingWithNoCallbackfnIsTheSameAsPassingUndefinedForCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsReferenceerrorIfCallbackfnIsUnreferenced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfCallbackfnIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfCallbackfnIsNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfCallbackfnIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfCallbackfnIsObjectWithoutCallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0EmptyArrayNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSideEffectsProducedByStep2WhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSideEffectsProducedByStep3WhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheExceptionIsNotThrownIfExceptionWasThrownByStep22()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheExceptionIsNotThrownIfExceptionWasThrownByStep32()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenToNullTypeConversionNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenToFalseTypeConversionNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversionNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversionNoInitval2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueofNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenWithObjWOValueofTostringNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenWithNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightInitialvalueIsReturnedIfLenIs0AndInitialvalueIsPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentEmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightInitialvalueIsPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenTo0TypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWith()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWith0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightNoObservableEffectsOccurIfLenIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightModificationsToLengthDonTChangeNumberOfIterationsInStep9()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightWhileLoopIsBreakenOnceKpresentIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsTheGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementChangedByGetterOnCurrentIterationIsObservedInSubsequentIterationsOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementChangedByGetterOnCurrentIterationIsObservedSubsequetlyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightExceptionInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightExceptionInGetterTerminateIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorWhenArrayIsEmptyAndInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorWhenElementsAssignedValuesAreDeletedByReducignArrayLengthAndInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThrowsTypeerrorWhenElementsAssignedValuesAreDeletedAndInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDoesnTThrowErrorWhenArrayHasNoOwnPropertiesButPrototypeContainsASingleProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheExceptionIsNotThrownIfExceptionWasThrownByStep23()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheExceptionIsNotThrownIfExceptionWasThrownByStep33()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDoesnTConsiderNewElementsWhichIndexIsLargerThanArrayOriginalLengthAddedToArrayAfterItIsCalledConsiderNewElementsWhichIndexIsSmallerThanArrayLength()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightConsidersNewValueOfElementsInArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDoesnTConsiderUnvisitedDeletedElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDoesnTConsiderUnvisitedDeletedElementsWhenArrayLengthIsDecreased()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnNotCalledForArrayWithOneElement()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightVisitsDeletedElementInArrayAfterTheCallWhenSameIndexIsAlsoPresentInPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightNotAffectCallWhenTheArrayIsDeletedDuringTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightNoObservableEffectsOccurIfLenIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightModificationsToLengthWillChangeNumberOfIterations()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightReturnsInitialvalueWhenArrayIsEmptyAndInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingPropertyOfPrototypeInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingPropertyOfPrototypeInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyWithPrototypePropertyInStep8CausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyWithPrototypePropertyInStep8CausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDecreasingLengthOfArrayInStep8CausesDeletedIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDecreasingLengthOfArrayWithPrototypePropertyInStep8CausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDecreasingLengthOfArrayInStep8DoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedIntoOwnObjectAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedIntoOwnObjectAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedToPrototypeAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAddedPropertiesInStep2AreVisibleHere2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedToPrototypeCanBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyCausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyCausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingPropertyOfPrototypeCausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingPropertyOfPrototypeCausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDecreasingLengthOfArrayCausesDeletedIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletedPropertiesInStep2AreVisibleHere2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedIntoOwnObjectInStep8CanBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedIntoOwnObjectInStep8CanBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedToPrototypeInStep8VisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightPropertiesAddedToPrototypeInStep8VisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDeletingOwnPropertyInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnNotCalledForIndexesNeverBeenAssignedValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsAnGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementChangedByGetterOnPreviousIterationsIsObservedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementChangedByGetterOnPreviousIterationsIsObservedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnCalledWithCorrectParametersInitialvalueNotPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnIsCalledWith1FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnIsCalledWith2FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnIsCalledWith3FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnIsCalledWith4FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnUsesArguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightNonIndexedPropertiesAreNotCalledOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAccumulatorUsedForCurrentIterationIsTheResultOfPreviousIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightAccumulatorUsedForFirstIterationIsTheValueOfInitialvalueWhenItIsPresentOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightValueOfAccumulatorUsedForFirstIterationIsTheValueOfMaxIndexPropertyWhichIsNotUndefinedWhenInitialvalueIsNotPresentOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnCalledWithCorrectParametersInitialvaluePassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightUndefinedCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightNullCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightBooleanPrimitiveCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightNumberPrimitiveCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightStringPrimitiveCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightFunctionObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightArrayObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightStringObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightBooleanObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightNumberObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnTakes4Arguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheMathObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightDateObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightRegexpObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheJsonCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightErrorObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheArgumentsObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightTheGlobalObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightUndefinedPassedAsThisvalueToStrictCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightKValuesArePassedInAcendingNumericOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightKValuesAreAccessedDuringEachIterationAndNotPriorToStartingTheLoopOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightUnhandledExceptionsHappenedInCallbackfnTerminateIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightElementChangedByCallbackfnOnPreviousIterationsIsObserved()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.22")]
        public void ArrayPrototypeReducerightCallbackfnIsCalledWith0FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-9.js", false);
        }


    }
}
