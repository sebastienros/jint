using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_21 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthMustBe1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDoesnTMutateTheArrayOnWhichItIsCalledOn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReducesTheArrayInAscendingOrderOfIndices()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceSubclassedArrayOfLength1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceSubclassedArrayWithLengthMoreThan1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReducesTheArrayInAscendingOrderOfIndicesInitialvaluePresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceSubclassedArrayWhenInitialvalueProvided()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceSubclassedArrayWithLength1AndInitialvalueProvided()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDoesnTVisitExpandos()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectLengthIsAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectThatLengthIsInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToTheArrayLikeObjectThatLengthPropertyDoesnTExist()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectLengthIsAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectLengthIsAnOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsNumberPrimitiveValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsABooleanValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorExceptionLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceUsesInheritedValueofMethodLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANumberValueIsPositive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANumberValueIsNegative()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheExceptionIsNotThrownIfExceptionWasThrownByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheExceptionIsNotThrownIfExceptionWasThrownByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallingWithNoCallbackfnIsTheSameAsPassingUndefinedForCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsReferenceerrorIfCallbackfnIsUnreferenced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfCallbackfnIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfCallbackfnIsNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfCallbackfnIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfCallbackfnIsObjectWithoutCallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0EmptyArrayNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceIfExceptionOccursItOccursAfterAnySideEffectsThatMightBeProducedByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceIfTheExceptionOccursItOccursAfterAnySideEffectsThatMightBeProducedByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheExceptionIsNotThrownIfExceptionWasThrownByStep22()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheExceptionIsNotThrownIfExceptionWasThrownByStep32()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenToNullTypeConversionNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenToFalseTypeConversionNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversionNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversionNoInitval2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueofNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenWithObjWOValueofTostringNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorIfLengthIs0SubclassedArrayLengthOverriddenWithNoInitval()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceInitialvalueIsReturnedIfLenIs0AndInitialvalueIsPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentEmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceInitialvalueIsPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenTo0TypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWith()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueIfLengthIs0AndInitialvalueIsPresentSubclassedArrayLengthOverriddenWith0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceNoObservableEffectsOccurIfLenIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceModificationsToLengthDonTChangeNumberOfIterationsInStep9()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceLoopIsBrokenOnceKpresentIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceWhenElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementChangedByGetterOnCurrentIterationsIsObservedInSubsequentIterationsOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementChangedByGetterOnCurrentIterationsIsObservedInSubsequentIterationsOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceExceptionInGetterTerminatesIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceExceptionInGetterTerminatesIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorWhenArrayIsEmptyAndInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorWhenElementsAssignedValuesAreDeletedByReducingArrayLengthAndInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThrowsTypeerrorWhenElementsAssignedValuesAreDeletedAndInitialvalueIsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDoesnTThrowErrorWhenArrayHasNoOwnPropertiesButPrototypeContainsASingleProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceIfExceptionOccursItOccursAfterAnySideEffectsThatMightBeProducedByStep22()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceIfExceptionOccursItOccursAfterAnySideEffectsThatMightBeProducedByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheExceptionIsNotThrownIfExceptionWasThrownByStep23()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheExceptionIsNotThrownIfExceptionWasThrownByStep33()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDoesnTConsiderNewElementsAddedToArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCalledWithAnInitialValueDoesnTConsiderNewElementsAddedToArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceConsidersNewValueOfElementsInArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDoesnTVisitDeletedElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDoesnTVisitDeletedElementsWhenArrayLengthIsDecreased()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnNotCalledForArrayWithOneElement()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceVisitsDeletedElementInArrayAfterTheCallWhenSameIndexIsAlsoPresentInPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceStopsCallingCallbackfnOnceTheArrayIsDeletedDuringTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceNoObservableEffectsOccurIfLenIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceModificationsToLengthDonTChangeNumberOfIterationsInStep92()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceReturnsInitialvalueWhenArrayIsEmptyAndInitialvalueIsPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingPropertyOfPrototypeInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingPropertyOfPrototypeInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyWithPrototypePropertyInStep8CausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyWithPrototypePropertyInStep8CausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDecreasingLengthOfArrayInStep8CausesDeletedIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDecreasingLengthOfArrayWithPrototypePropertyInStep8CausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDecreasingLengthOfArrayInStep8DoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesAddedIntoOwnObjectAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesAddedIntoOwnObjectAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesAddedToPrototypeAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAddedPropertiesInStep2AreVisibleHere2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesCanBeAddedToPrototypeAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyCausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyCausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingPropertyOfPrototypeCausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingPropertyOfPrototypeCausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDecreasingLengthOfArrayCausesDeletedIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletedPropertiesInStep2AreVisibleHere2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesAddedIntoOwnObjectInStep8AreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesAddedIntoOwnObjectInStep8AreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesAddedToPrototypeInStep8AreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReducePropertiesAddedToPrototypeInStep8AreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDeletingOwnPropertyInStep8CausesDeletedIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnNotCalledForIndexesNeverBeenAssignedValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheGlobalObjectWhichContainsIndexProperty2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementChangedByGetterOnPreviousIterationsIsObservedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementChangedByGetterOnPreviousIterationsIsObservedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnCalledWithCorrectParametersInitialvalueNotPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnIsCalledWith1FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnIsCalledWith2FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnIsCalledWith3FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnIsCalledWith4FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnThatUsesArguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceNonIndexedPropertiesAreNotCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceAccumulatorUsedForCurrentIterationIsTheResultOfPreviousIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfAccumulatorUsedForFirstIterationIsTheValueOfInitialvalueWhenItIsPresentOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceValueOfAccumulatorUsedForFirstIterationIsTheValueOfLeastIndexPropertyWhichIsNotUndefinedWhenInitialvalueIsNotPresentOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnCalledWithCorrectParametersInitialvaluePassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceUndefinedCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceNullCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceBooleanPrimitiveCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceNumberPrimitiveCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceStringPrimitiveCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceFunctionObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceArrayObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceStringObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceBooleanObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceNumberObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnTakes4Arguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheMathObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceDateObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceRegexpObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheJsonCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceErrorObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheArgumentsObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceTheGlobalObjectCanBeUsedAsAccumulator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceUndefinedPassedAsThisvalueToStrictCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceKValuesArePassedInAcendingNumericOrderOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceKValuesAreAccessedDuringEachIterationAndNotPriorToStartingTheLoopOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceUnhandledExceptionsHappenedInCallbackfnTerminateIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceElementChangedByCallbackfnOnPreviousIterationsIsObserved()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.21")]
        public void ArrayPrototypeReduceCallbackfnIsCalledWith0FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-9.js", false);
        }


    }
}
