using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofHasALengthPropertyWhoseValueIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToUndefinedThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToNullThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1ForElementsNotPresentInArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-10-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0AndDoesNotAccessAnyOtherProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-10-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsUndefinedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsNumberPrimitiveValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAStringContainingAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturn1WhenLengthIsABooleanValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANumberValueIsPositive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANumberValueIsNegative()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0EmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsANumberOfValue6E1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0LengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0LengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0GenericArrayWithLength0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0LengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0LengthIsObjectOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfLengthIs0LengthIsAnEmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofLengthIsANumberOfValue01()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofWhenFromindexIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIsPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIsNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAStringContainingInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofWhenFromindexIsFloatingPointNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexWhichIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofThrowsTypeerrorExceptionWhenValueOfFromindexIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofUsesInheritedValueofMethodWhenValueOfFromindexIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSideEffectsProducedByStep1AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofWhenFromindexIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofFromindexIsAPositiveNonIntegerVerifyTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofFromindexIsANegativeNonIntegerVerifyTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMatchOnTheFirstElementAMiddleElementAndTheLastElementWhenFromindexIsPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns0IfFromindexIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns0IfFromindexIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofFromindexIsnTPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofValueOfFromindexIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1IfFromindexIsGreaterThanArrayLength()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1WhenFromindexIsLengthOfArray1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturnsCorrectIndexWhenFromindexIsLengthOfArray1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1WhenFromindexAndLengthAreBoth0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1WhenFromindexIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturnsCorrectIndexWhenFromindexIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofWithNegativeFromindex()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturnsCorrectIndexWhenFromindexIs12()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1WhenAbsFromindexIsLengthOfArray1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-8-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturns1WhenAbsFromindexIsLengthOfArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-8-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void NoteThatPriorToTheFinallyEs5DraftSamevalueWasUsedForComparisionsAndHenceNansCouldBeFoundUsingIndexof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofTheLengthOfIterationIsnTChangedByAddingElementsToTheArrayDuringIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexSelfReference()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofMustReturnCorrectIndexSparseArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDecreasingLengthOfArrayCausesIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAddedPropertiesInStep5AreVisibleHereOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAddedPropertiesInStep5AreVisibleHereOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletedPropertiesInStep5AreVisibleHereOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofDeletedPropertiesInStep5AreVisibleHereOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofNonExistentPropertyWouldnTBeCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofThisObjectIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsToNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSideEffectsAreVisibleInSubsequentIterationsOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSideEffectsAreVisibleInSubsequentIterationsOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofTerminatesIterationOnUnhandledExceptionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofTerminatesIterationOnUnhandledExceptionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofTypeOfArrayElementIsDifferentFromTypeOfSearchElement()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofBothArrayElementAndSearchElementAreBooleanTypeAndTheyHaveSameValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofBothArrayElementAndSearchElementAreObjectTypeAndTheyReferToTheSameObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofBothTypeOfArrayElementAndTypeOfSearchElementAreUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofBothTypeOfArrayElementAndTypeOfSearchElementAreNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSearchElementIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofSearchElementIsNan2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofArrayElementIs0AndSearchElementIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofArrayElementIs0AndSearchElementIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofBothArrayElementAndSearchElementAreNumberAndTheyHaveSameValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofBothArrayElementAndSearchElementAreStringAndTheyHaveExactlyTheSameSequenceOfCharacters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturnsIndexOfLastOneWhenMoreThanTwoElementsInArrayAreEligible()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-iii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.14")]
        public void ArrayPrototypeIndexofReturnsWithoutVisitingSubsequentElementOnceSearchValueIsFound()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-iii-2.js", false);
        }


    }
}
