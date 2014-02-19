using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_15 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofHasALengthPropertyWhoseValueIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToUndefinedThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToNullThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsUndefinedPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsPropertyOfTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToStringObjectWhichImplementsItsOwnPropertyGetMethod2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenLengthIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringContainingPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringContainingNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringContainingADecimalNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringWhichIsAbleToBeConvertedIntoHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAStringThatCanTConvertToANumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturn1WhenValueOfLengthIsABooleanValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofThrowsTypeerrorExceptionWhenLengthIsAnObjectWithTostringAndValueofMethodsThatDonTReturnPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofUsesInheritedValueofMethodWhenLengthIsAnObjectWithAnOwnTostringAndAnInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsAPositiveNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANegativeNonIntegerEnsureTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsBoundaryValue232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsBoundaryValue2321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIsAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIsANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfLengthIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0EmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsANumberOfValue6E1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0LengthOverriddenToNullTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0LengthOverriddenToFalseTypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0GenericArrayWithLength0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0LengthOverriddenTo0TypeConversion()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0SubclassedArrayLengthOverriddenWithObjWithValueof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0LengthIsObjectOverriddenWithObjWOValueofTostring()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0LengthIsAnEmptyArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofLengthIsANumberOfValue01()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenFromindexIsString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIsPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIsNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsAStringContainingANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsAStringContainingInfinity()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsAStringContainingInfinity2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsAStringContainingAnExponentialNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsAStringContainingAHexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenFromindexIsFloatingPointNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexWhichIsAStringContainingANumberWithLeadingZeros()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexWhichIsAnObjectAndHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexWhichIsAnObjectAndHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsAnObjectThatHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofThrowsTypeerrorExceptionWhenValueOfFromindexIsAnObjectThatBothTostringAndValueofMethodsThanDonTReturnPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofUseInheritedValueofMethodWhenValueOfFromindexIsAnObjectWithAnOwnTostringAndInheritedValueofMethods()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSideEffectsProducedByStep1AreVisibleWhenAnExceptionOccurs()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSideEffectsProducedByStep2AreVisibleWhenAnExceptionOccurs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenFromindexIsBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSideEffectsProducedByStep3AreVisibleWhenAnExceptionOccurs2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofFromindexIsAPositiveNonIntegerVerifyTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofFromindexIsANegativeNonIntegerVerifyTruncationOccursInTheProperDirection()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMatchOnTheFirstElementAMiddleElementAndTheLastElementWhenFromindexIsPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenFromindexIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenFromindexIsNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenFromindexIsnTPassed()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofValueOfFromindexIsANumberValueIs03()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWhenFromindexGreaterThanArrayLength()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturnsCorrectIndexWhenFromindexIsLengthOfArray1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1WhenFromindexIsLengthOfArray1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1WhenFromindexAndLengthAreBoth0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1WhenFromindexIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturnsCorrectIndexWhenFromindexIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofWithNegativeFromindex()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturnsCorrectIndexWhenFromindexIs12()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-7-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1WhenAbsFromindexIsLengthOfArray1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-7-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1WhenAbsFromindexIsLengthOfArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-7-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexBoolean()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void NoteThatPriorToTheFinallyEs5DraftSamevalueWasUsedForComparisionsAndHenceNansCouldBeFoundUsingLastindexof()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofTheLengthOfIterationIsnTChangedByAddingElementsToTheArrayDuringIteration()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexNumber()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexSelfReference()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofMustReturnCorrectIndexSparseArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAddedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletingOwnPropertyCausesIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletingPropertyOfPrototypeCausesPrototypeIndexPropertyNotToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletingOwnPropertyWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDecreasingLengthOfArrayCausesIndexPropertyNotToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDecreasingLengthOfArrayWithPrototypePropertyCausesPrototypeIndexPropertyToBeVisited()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDecreasingLengthOfArrayDoesNotDeleteNonConfigurableProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAddedPropertiesInStep5AreVisibleHereOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAddedPropertiesInStep5AreVisibleHereOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletedPropertiesInStep2AreVisibleHere()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletedPropertiesOfStep5AreVisibleHereOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofDeletedPropertiesOfStep5AreVisibleHereOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofPropertiesAddedIntoOwnObjectAfterCurrentPositionAreVisitedOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofPropertiesCanBeAddedToPrototypeAfterCurrentPositionAreVisitedOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofUndefinedPropertyWouldnTBeCalled()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsAnOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsInheritedAccessorPropertyWithoutAGetFunctionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofThisObjectIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsLessThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsEqualsToNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofAppliedToArgumentsObjectWhichImplementsItsOwnPropertyGetMethodNumberOfArgumentsIsGreaterThanNumberOfParameters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSideEffectsAreVisibleInSubsequentIterationsOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSideEffectsAreVisibleInSubsequentIterationsOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofTerminatesIterationOnUnhandledExceptionOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofTerminatesIterationOnUnhandledExceptionOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsInheritedDataPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsInheritedDataPropertyOnAnArrayLikeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofElementToBeRetrievedIsOwnAccessorPropertyOnAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofTypeOfArrayElementIsDifferentFromTypeOfSearchElement()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofBothArrayElementAndSearchElementAreBooleansAndTheyHaveSameValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofBothArrayElementAndSearchElementAreObjectsAndTheyReferToTheSameObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofBothTypeOfArrayElementAndTypeOfSearchElementAreUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofBothTypeOfArrayElementAndTypeOfSearchElementAreNull()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSearchElementIsNan()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofSearchElementIsNan2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofArrayElementIs0AndSearchElementIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofArrayElementIs0AndSearchElementIs02()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofBothArrayElementAndSearchElementAreNumbersAndTheyHaveSameValue()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofBothArrayElementAndSearchElementAreStringsAndTheyHaveExactlyTheSameSequenceOfCharacters()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturnsIndexOfLastOneWhenMoreThanTwoElementsInArrayAreEligible()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-iii-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturnsWithoutVisitingSubsequentElementOnceSearchValueIsFound()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-iii-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1ForElementsNotPresent()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.15")]
        public void ArrayPrototypeLastindexofReturns1IfLengthIs0AndDoesNotAccessAnyOtherProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-9-2.js", false);
        }


    }
}
