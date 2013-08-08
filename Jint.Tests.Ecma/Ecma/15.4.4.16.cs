using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_16 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthMustBe1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToUndefinedThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToNullThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToTheArrayLikeObjectThatLengthIsInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToTheArrayLikeObjectThatLengthPropertyDoesnTExist()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryOnAnArrayLikeObjectIfLengthIs1LengthOverriddenToTrueTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIsPositive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIsNegative()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThrowsTypeerrorIfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryTheExceptionIsNotThrownIfExceptionWasThrownByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryTheExceptionIsNotThrownIfExceptionWasThrownByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallingWithNoCallbackfnIsTheSameAsPassingUndefinedForCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThrowsTypeerrorIfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThrowsTypeerrorIfCallbackfnIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThrowsTypeerrorIfCallbackfnIsNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThrowsTypeerrorIfCallbackfnIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThrowsTypeerrorIfCallbackfnIsObjectWithoutACallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEverySideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEverySideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisargNotPassedToStrictCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisargNotPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryArrayObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryStringObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryBooleanObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryNumberObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryTheMathObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDateObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryRegexpObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryTheJsonObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryErrorObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryTheArgumentsObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisargIsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryTheGlobalObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryBooleanPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryNumberPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryStringPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisargIsArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisargIsObjectFromObjectTemplatePrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisargIsObjectFromObjectTemplate()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisargIsFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryBuiltInFunctionsCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryFunctionObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryConsidersNewElementsAddedToArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryConsidersNewValueOfElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDoesnTVisitDeletedElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDoesnTVisitDeletedElementsWhenArrayLengthIsDecreased()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDoesnTConsiderNewlyAddedElementsInSparseArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryVisitsDeletedElementInArrayAfterTheCallWhenSameIndexIsAlsoPresentInPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletingTheArrayItselfWithinTheCallbackfnOfArrayPrototypeEveryIsSuccessfulOnceArrayPrototypeEveryIsCalledForAllElements()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryNoObservableEffectsOccurIfLenIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryModificationsToLengthDonTChangeNumberOfIterations()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnNotCalledForIndexesNeverBeenAssignedValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDecreasingLengthOfArrayCausesIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisObjectIsAnGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementChangedByGetterOnPreviousIterationsIsObservedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementChangedByGetterOnPreviousIterationsIsObservedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryUnhandledExceptionsHappenedInGetterTerminateIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnCalledWithCorrectParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnIsCalledWith1FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnIsCalledWith2FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnIsCalledWith3FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnThatUsesArgumentsObjectToGetParameterValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisOfCallbackfnIsABooleanObjectWhenTIsNotAnObjectTIsABooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisOfCallbackfnIsANumberObjectWhenTIsNotAnObjectTIsANumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryThisOfCallbackfnIsAnStringObjectWhenTIsNotAnObjectTIsAStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryNonIndexedPropertiesAreNotCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnTakes3Arguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnCalledWithCorrectParametersThisargIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnCalledWithCorrectParametersKvalueIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnCalledWithCorrectParametersTheIndexKIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnCalledWithCorrectParametersThisObjectOIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryImmediatelyReturnsFalseIfCallbackfnReturnsFalse()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryKValuesArePassedInAscendingNumericOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryKValuesAreAccessedDuringEachIterationAndNotPriorToStartingTheLoopOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryArgumentsToCallbackfnAreSelfConsistent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryUnhandledExceptionsHappenedInCallbackfnTerminateIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryElementChangedByCallbackfnOnPreviousIterationsIsObserved()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryCallbackfnIsCalledWith0FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANonEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsAStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsADateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryFalsePreventsFurtherSideEffects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueNewBooleanFalseOfCallbackfnIsTreatedAsTrueValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsABooleanValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsABooleanValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANunmberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberValueIsPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnValueOfCallbackfnIsANumberValueIsNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0EmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEverySubclassedArrayWhenLengthIsReduced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueWhenAllCallsToCallbackfnReturnTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDoesnTMutateTheArrayOnWhichItIsCalledOn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryDoesnTVisitExpandos()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0SubclassedArrayLengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0SubclassedArrayLengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0SubclassedArrayLengthOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.16")]
        public void ArrayPrototypeEveryReturnsTrueIfLengthIs0SubclassedArrayLengthOverriddenWith()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-8.js", false);
        }


    }
}
