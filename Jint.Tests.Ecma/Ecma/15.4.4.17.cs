using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_17 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthMustBe1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToUndefinedThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToNullThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthPropertyDoesnTExistOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeOnAnArrayLikeObjectIfLengthIs1LengthOverriddenToTrueTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIsPositive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIsNegative()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsTypeerrorIfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeTheExceptionIsNotThrownIfExceptionWasThrownByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeTheExceptionIsNotThrownIfExceptionWasThrownByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallingWithNoCallbackfnIsTheSameAsPassingUndefinedForCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsReferenceerrorIfCallbackfnIsUnreferenced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsTypeerrorIfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsTypeerrorIfCallbackfnIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsTypeerrorIfCallbackfnIsNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsTypeerrorIfCallbackfnIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThrowsTypeerrorIfCallbackfnIsObjectWithoutACallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargNotPassedToStrictCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargIsPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeArrayObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeStringObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeBooleanObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeNumberObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeTheMathObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDateObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeRegexpObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeTheJsonObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeErrorObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeTheArgumentsObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargIsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeTheGlobalObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeBooleanPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeNumberPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeStringPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargNotPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargIsArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargIsObjectFromObjectTemplatePrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargIsObjectFromObjectTemplate()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisargIsFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeBuiltInFunctionsCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeFunctionObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeConsidersNewElementsAddedToArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeConsidersNewValueOfElementsInArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDoesnTVisitDeletedElementsInArrayAfterItIsCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDoesnTVisitDeletedElementsWhenArrayLengthIsDecreased()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDoesnTConsiderNewlyAddedElementsInSparseArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeVisitsDeletedElementInArrayAfterTheCallWhenSameIndexIsAlsoPresentInPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeNoObservableEffectsOccurIfLengthIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeModificationsToLengthDonTChangeNumberOfIterations()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnNotCalledForIndexesNeverBeenAssignedValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDecreasingLengthOfArrayCausesIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomePropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomePropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomePropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomePropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisObjectIsAnGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementChangedByGetterOnPreviousIterationsIsObservedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementChangedByGetterOnPreviousIterationsOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeUnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeUnhandledExceptionsHappenedInGetterTerminateIterationOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnCalledWithCorrectParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnIsCalledWith1FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnIsCalledWith2FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnIsCalledWith3FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnThatUsesArgumentsObjectToGetParameterValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisOfCallbackIsABooleanObjectWhenTIsNotAnObjectTIsABooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisOfCallbackfnIsANumberObjectWhenTIsNotAnObjectTIsANumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeThisOfCallbackfnIsAnStringObjectWhenTIsNotAnObjectTIsAStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeNonIndexedPropertiesAreNotCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnTakes3Arguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnCalledWithCorrectParametersThisargIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnCalledWithCorrectParametersKvalueIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnCalledWithCorrectParametersTheIndexKIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnCalledWithCorrectParametersThisObjectOIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeImmediatelyReturnsTrueIfCallbackfnReturnsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeKValuesArePassedInAscendingNumericOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeKValuesAreAccessedDuringEachIterationAndNotPriorToStartingTheLoop()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeArgumentsToCallbackfnAreSelfConsistent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeUnhandledExceptionsHappenedInCallbackfnTerminateIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeElementChangedByCallbackfnOnPreviousIterationsIsObserved()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeCallbackfnIsCalledWith0FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANonEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsAStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsADateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeTruePreventsFurtherSideEffects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueNewBooleanFalseOfCallbackfnIsTreatedAsTrueValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsABooleanValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsABooleanValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIsPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnValueOfCallbackfnIsANumberValueIsNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0EmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeSubclassedArrayWhenLengthIsReduced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseWhenAllCallsToCallbackfnReturnFalse()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDoesnTMutateTheArrayOnWhichItIsCalledOn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeDoesnTVisitExpandos()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0SubclassedArrayLengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0SubclassedArrayLengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0SubclassedArrayLengthOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.17")]
        public void ArrayPrototypeSomeReturnsFalseIfLengthIs0SubclassedArrayLengthOverriddenWith()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-8.js", false);
        }


    }
}
