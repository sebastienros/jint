using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_18 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthMustBe1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToArrayLikeObjectLengthIsAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToTheArrayLikeObjectThatLengthIsInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToTheArrayLikeObjectThatLengthPropertyDoesnTExist()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAnOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToArrayLikeObjectLengthIsAnOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToArrayLikeObjectLengthIsAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToArrayLikeObjectLengthIsAnOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAppliedToArrayLikeObjectLengthIsAnOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsABooleanValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIsPositive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIsNegative()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsTypeerrorIfCallbackfnIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachTheExceptionIsNotThrownIfExceptionWasThrownByStep2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachTheExceptionIsNotThrownIfExceptionWasThrownByStep3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallingWithNoCallbackfnIsTheSameAsPassingUndefinedForCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsReferenceerrorIfCallbackfnIsUnreferenced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsTypeerrorIfCallbackfnIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsTypeerrorIfCallbackfnIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsTypeerrorIfCallbackfnIsNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsTypeerrorIfCallbackfnIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThrowsTypeerrorIfCallbackfnIsObjectWithoutCallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargNotPassedToStrictCallbackfn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargIsPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachArrayObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachStringObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachBooleanObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachNumberObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachTheMathObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDateObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachRegexpObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachTheJsonObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachErrorObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachTheArgumentsObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargIsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachTheGlobalObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachBooleanPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachNumberPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachStringPrimitiveCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargNotPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargIsArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargIsObjectFromObjectTemplatePrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargIsObjectFromObjectTemplate()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisargIsFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachBuiltInFunctionsCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachFunctionObjectCanBeUsedAsThisarg()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTConsiderNewElementsAddedToArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTVisitDeletedElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTVisitDeletedElementsWhenArrayLengthIsDecreased()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTConsiderNewlyAddedElementsInSparseArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachVisitsDeletedElementInArrayAfterTheCallWhenSameIndexIsAlsoPresentInPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachConsidersNewValueOfElementsInArrayAfterTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachNoObservableEffectsOccurIfLenIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachModificationsToLengthDonTChangeNumberOfIterations()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnNotCalledForIndexesNeverBeenAssignedValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDecreasingLengthOfArrayCausesIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisObjectIsAnGlobalObjectWhichContainsIndexProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisObjectIsTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementChangedByGetterOnPreviousIterationsIsObservedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementChangedByGetterOnPreviousIterationsIsObservedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachUnnhandledExceptionsHappenedInGetterTerminateIterationOnAnArrayLikeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnCalledWithCorrectParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnIsCalledWith1FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnIsCalledWith2FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnIsCalledWith3FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnThatUsesArguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisOfCallbackfnIsABooleanObjectWhenTIsNotAnObjectTIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisOfCallbackfnIsANumberObjectWhenTIsNotAnObjectTIsANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachThisOfCallbackfnIsAnStringObjectWhenTIsNotAnObjectTIsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachNonIndexedPropertiesAreNotCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnTakes3Arguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnCalledWithCorrectParametersThisargIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnCalledWithCorrectParametersKvalueIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnCalledWithCorrectParametersTheIndexKIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnCalledWithCorrectParametersThisObjectOIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachKValuesArePassedInAscendingNumericOrder()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachKValuesAreAccessedDuringEachIterationAndNotPriorToStartingTheLoopOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachArgumentsToCallbackfnAreSelfConsistent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachUnhandledExceptionsHappenedInCallbackfnTerminateIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachElementChangedByCallbackfnOnPreviousIterationsIsObserved()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachCallbackfnIsCalledWith0FormalParameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0EmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachSubclassedArrayWhenLengthIsReduced()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTMutateTheArrayOnWhichItIsCalledOn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTVisitExpandos()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachUndefinedWillBeReturnedWhenLenIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenTo0TypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenWith()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayPrototypeForeachDoesnTCallCallbackfnIfLengthIs0SubclassedArrayLengthOverriddenWith0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayForeachCanBeFrozenWhileInProgress()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/S15.4.4.18_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.18")]
        public void ArrayForeachCanBeFrozenWhileInProgress2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.18/S15.4.4.18_A2.js", false);
        }


    }
}
