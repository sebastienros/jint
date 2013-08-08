using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyMustExistAsAFunctionTaking3Parameters()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAppliedToUndefinedThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAppliedToNullThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAppliedToNumberPrimitiveThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAppliedToStringPrimitiveThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsUndefinedThatConvertsToStringUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIsANegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIsInfinity3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Following20Zeros()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Following21Zeros()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Following22Zeros()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Trailing5Zeros()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-17-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1E20()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToStringValueIs1E21()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToStringValueIs1E22()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsNullThatConvertsToStringNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs0000001()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs00000001()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs000000001()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1E7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1E6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1E5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAnIntegerThatConvertsToAStringValueIs123()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsADecimalThatConvertsToAStringValueIs123456()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Following19Zeros1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Following20Zeros1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsABooleanWhoseValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Following21Zeros1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1Following22Zeros1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs1231234567()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAppliedToAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAppliedToStringAbCd()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAppliedToStringUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAppliedToStringNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAppliedToString123Cd()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAppliedToString1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAnArrayThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsABooleanWhoseValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAStringObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsABooleanObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAnObjectThatHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAnObjectThatHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAnObjectWhoseTostringMethodReturnsAnObjectAndWhoseValueofMethodReturnsAPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsAnObjectThatHasAnOwnTostringAndValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyTypeerrorExceptionIsThrownWhenPIsAnObjectThatNeitherTostringNorValueofReturnsAPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAnInheritedTostringMethodIsInvokedWhenPIsAnObjectWithAnOwnValueofAndAnInheritedTostringMethods()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs02()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIs03()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyArgumentPIsANumberThatConvertsToAStringValueIsAPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsNull8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-100.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsTrue8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-101.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsFalse8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-102.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIs08105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-103.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIs08105Step4B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-104.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIs08105Step4B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-105.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsNan8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-106.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAPositiveNumber8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-107.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsANegativeNumber8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-108.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAnEmptyString8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-109.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsANonEmptyString8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-110.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAFunctionObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-111.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAnArrayObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-112.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAStringObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-113.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsABooleanObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-114.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsANumberObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-115.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsTheMathObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-116.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsADateObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-117.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsARegexpObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-118.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsTheJsonObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-119.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAErrorObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-120.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsTheArgumentObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-121.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsTheGlobalObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-123.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsTreatedAsTrueWhenItIsAStringValueIsFalse8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-124.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsTreatedAsTrueWhenItIsNewBooleanFalse8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-125.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsPresent8105Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-126.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsNotPresent8105Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-127.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-129.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-130.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-131.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsOwnAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-132.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-133.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-134.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-135.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunction8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-136.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-137.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValuePropertyInAttributesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-138.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-139-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-139.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-140-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-140.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-141-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-141.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-142-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-142.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-143-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-143.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-144-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-144.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-145-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-145.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-146-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-146.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-147-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-147.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-148-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-148.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheValuePropertyOfPrototypeObject8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-149-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-149.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsUndefined8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-151.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsPresent8105Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-152.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsNotPresent8105Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-153.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-154.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-155.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-156.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-157.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-158.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-159.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsNull8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-160.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-161.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunction8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-162.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-163.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-164.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-165-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-165.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-166-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-166.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-167-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-167.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-168-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-168.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-169-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-169.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanPrimitive8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-170-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-170.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-171-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-171.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-172-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-172.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-173-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-173.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-174-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-174.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheWritablePropertyOfPrototypeObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-175-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-175.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-177.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsUndefined8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-178.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsNull8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-179.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberPrimitive8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsTrue8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-180.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsFalse8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-181.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIs08105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-182.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIs08105Step6B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-183.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIs08105Step6B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-184.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsNan8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-185.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAPositiveNumber8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-186.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsANegativeNumber8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-187.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAnEmptyString8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-188.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsANonEmptyString8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-189.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringPrimitive8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAFunctionObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-190.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAnArrayObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-191.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAStringObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-192.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsABooleanObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-193.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsANumberObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-194.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsTheMathObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-195.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsADateObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-196.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsARegexpObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-197.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsTheJsonObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-198.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsAErrorObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-199.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsPresent8105Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsTheArgumentObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-200.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsTheGlobalObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-202.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsTreatedAsTrueWhenItIsAStringValueIsFalse8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-203.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWritablePropertyInAttributesIsTreatedAsTrueWhenItIsNewBooleanFalse8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-204.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsPresent8105Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-205.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsNotPresent8105Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-206.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-207.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-208.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-209.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsNotPresent8105Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-210.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-211.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-212.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-213.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-214.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunction8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-215.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-216.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyGetPropertyInAttributesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-217.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-218-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-218.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-219-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-219.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-220-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-220.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-221-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-221.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-222-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-222.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-223-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-223.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-224-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-224.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-225-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-225.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-226-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-226.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-227-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-227.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheGetPropertyOfPrototypeObject8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-228-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-228.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-230.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfGetPropertyInAttributesIsUndefined8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-231.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfGetPropertyInAttributesIsAFunction8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-232.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsPresent8105Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-235.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsNotPresent8105Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-236.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-237.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-238.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-239.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-240.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-241.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-242.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-243.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-244.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunction8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-245.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-246.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertySetPropertyInAttributesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-247.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-248-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-248.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-249-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-249.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-250-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-250.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-251-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-251.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-252-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-252.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-253-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-253.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-254-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-254.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-255-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-255.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-256-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-256.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-257-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-257.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheSetPropertyOfPrototypeObject8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-258-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-258.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-260.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfSetPropertyInAttributesIsUndefined8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-261.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfSetPropertyInAttributesIsAFunction8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-262.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunction8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyEnumerablePropertyInAttributesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-33-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-34-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-35-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-36-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-37-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-38-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-39-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements9()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnRegexpObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-40-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnRegexpObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-41-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-42-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheEnumerablePropertyOfPrototypeObject8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-43-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsUndefined8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsNull8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsTrue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsFalse8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIs08105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIs08105Step3B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIs08105Step3B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-52.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsNan8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-53.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsAPositiveNumber8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-54.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsANegativeNumber8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-55.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsAnEmptyString8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-56.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsANonEmptyString8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-57.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsAFunctionObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-58.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsAnArrayObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-59.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsAStringObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-60.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsABooleanObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-61.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsANumberObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-62.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsTheMathObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-63.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsADateObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-64.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsARegexpObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-65.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsTheJsonObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-66.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsAnErrorObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-67.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsAnArgumentsObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-68.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsTheGlobalObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-70.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsTreatedAsTrueWhenItIsAStringValueIsFalse8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-71.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyValueOfEnumerablePropertyInAttributesIsNewBooleanFalseWhichIsTreatedAsTrueValue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-72.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsPresent8105Step4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-73.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsNotPresent8105Step4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-74.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-75.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-76.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-77.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-78.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-79.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements13()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-80.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-81.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-82.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunction8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-83.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-84.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-85.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheConfigurablePropertyOfPrototypeObject8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-86-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-86.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-87-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-87.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-88-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAStringObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-88.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-89-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-89.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void TheAbtractOperationTopropertydescriptorIsUsedToPackageTheIntoAPropertyDescStep10OfTopropertydescriptorThrowsATypeerrorIfThePropertyDescEndsUpHavingAMixOfAccessorAndDataPropertyElements14()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-90-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsANumberObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-90.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-91-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-91.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-92-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsADateObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-92.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnRegexpObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-93-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnRegexpObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-93.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-94-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-94.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-95-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-95.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-96-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-96.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyAttributesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-98.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyConfigurablePropertyInAttributesIsUndefined8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-99.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOPassingTrueForTheThrowFlagInThisCaseStep3OfDefineownpropertyRequiresThatItThrowATypeerrorExceptionWhenCurrentIsUndefinedAndExtensibleIsFalseTheValueOfDescDoesNotMatter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyStep7BOfDefineownpropertyRejectsIfCurrentEnumerableAndDescEnumerableAreTheBooleanNegationsOfEachOther()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreDataPropertiesDescValueAndNameValueAreTwoDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-100.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreDataPropertiesNameValueIsPresentAndDescValueIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-101.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreDataPropertiesDescValueIsPresentAndNameValueIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-102.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreDataPropertiesNameWritableAndDescWritableAreDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-103.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreDataPropertiesNameEnumerableAndDescEnumerableAreDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-104.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreDataPropertiesNameConfigurableTrueAndDescConfigurableFalse8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-105.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreDataPropertiesSeveralAttributesValuesOfNameAndDescAreDifferent8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-106.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesBothDescGetAndNameGetAreTwoDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-107.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesNameGetIsPresentAndDescGetIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-108.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesNameGetIsUndefinedAndDescGetIsFunction8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-109.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyStep7BOfDefineownpropertyRejectsIfCurrentEnumerableAndDescEnumerableAreTheBooleanNegationsOfEachOther2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesBothDescSetAndNameSetAreTwoDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-110.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesNameSetIsPresentAndDescSetIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-111.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesNameSetIsUndefinedAndDescSetIsFunction8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-112.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesNameEnumerableAndDescEnumerableAreDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-113.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesNameConfigurableTrueAndDescConfigurableFalse8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-114.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameAndDescAreAccessorPropertiesSeveralAttributesValuesOfNameAndDescAreDifferent8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-115.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayTestTheLengthPropertyOfOIsOwnDataProperty15451Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-116.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayTestTheLengthPropertyOfOIsOwnDataPropertyThatOverridesAnInheritedDataProperty15451Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-117.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestEveryFieldInDescIsAbsent15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-118.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestEveryFieldInDescIsSameWithCorrespondingAttributeValueOfTheLengthPropertyInO15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-119.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep9AOfDefineownpropertyRejectsChangingTheKindOfAProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeOfTheLengthPropertyFromFalseToTrue15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-120.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeOfTheLengthPropertyFromFalseToTrue15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-121.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTypeerrorIsThrownWhenDescIsAccessorDescriptor15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-122.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeOfTheLengthPropertyFromFalseToTrue15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-123.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestUpdatingTheWritableAttributeOfTheLengthPropertyFromTrueToFalse15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-124.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestThatRangeerrorExceptionIsThrownWhenValueFieldOfDescIsUndefined15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-125.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsNull15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-126.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsABooleanWithValueFalse15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-127.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsABooleanWithValueTrue15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-128.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsNotThrownWhenTheValueFieldOfDescIs015451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-129.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep9AOfDefineownpropertyRejectsChangingTheKindOfAProperty2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsNotThrownWhenTheValueFieldOfDescIs015451Step3C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-130.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsNotThrownWhenTheValueFieldOfDescIs015451Step3C3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-131.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsNotThrownWhenTheValueFieldOfDescIsAPositiveNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-132.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsANegativeNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-133.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsInfinity15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-134.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsInfinity15451Step3C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-135.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsNan15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-136.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsNotThrownWhenTheValueFieldOfDescIsAStringContainingAPositiveNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-137.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsAStringContainingANegativeNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-138.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsAStringContainingADecimalNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-139.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForConfigurablePropertiesStep9BOfDefineownpropertyPermitsChangingTheKindOfAProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsAStringContainingInfinity15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-140.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsAStringContainingInfinity15451Step3C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-141.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingAnExponentialNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-142.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingAHexNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-143.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingANumberWithLeadingZeros15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-144.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorExceptionIsThrownWhenTheValueFieldOfDescIsAStringWhichDoesnTConvertToANumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-145.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnTostringMethod15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-146.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnValueofMethod15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-147.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-148.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnTostringAndValueofMethod15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-149.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForConfigurablePropertiesStep9COfDefineownpropertyPermitsChangingTheKindOfAProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTypeerrorIsThrownWhenTheValueFieldOfDescIsAnObjectThatBothTostringAndValueofWouldnTReturnPrimitiveValue15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-150.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOAndTheValueFieldOfDescIsAnObjectWithAnOwnTostringMethodAndAnInheritedValueofMethod15451Step3CTestThatTheInheritedValueofMethodIsUsed()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-151.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsAPositiveNonIntegerValues15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-152.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsANegativeNonIntegerValues15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-153.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsBoundaryValue232215451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-154.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsBoundaryValue232115451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-155.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsBoundaryValue23215451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-156.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsBoundaryValue232115451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-157.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOSetTheValueFieldOfDescToAValueGreaterThanTheExistingValueOfLength15451Step3F()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-159.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep10AIOfDefineownpropertyRejectsIfRelaxingTheWritableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOSetTheValueFieldOfDescToAValueEqualToTheExistingValueOfLength15451Step3F()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-160.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOSetTheValueFieldOfDescToAValueLesserThanTheExistingValueOfLengthAndTestThatIndexesBeyondTheNewLengthAreDeleted15451Step3F()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-161.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsGreaterThanValueOfTheLengthPropertyTestTypeerrorIsThrownWhenTheLengthPropertyIsNotWritable15451Step3FI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-162.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescEqualsToValueOfTheLengthPropertyTestNoTypeerrorIsThrownWhenTheLengthPropertyIsNotWritable15451Step3FI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-163.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTypeerrorIsThrownWhenTheWritableAttributeOfTheLengthPropertyIsFalse15451Step3G()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-164.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToTrueAfterDeletingPropertiesWithLargeIndexNamedIfTheWritableFieldOfDescIsAbsent15451Step3H()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-165.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToTrueAfterDeletingPropertiesWithLargeIndexNamedIfTheWritableFieldOfDescIsTrue15451Step3H()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-166.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToFalseAfterDeletingPropertiesWithLargeIndexNamedIfTheWritableFieldOfDescIsFalse15451Step3IIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-167.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOWhoseWritableAttributeIsBeingChangedToFalseAndTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyAndAlsoLesserThanAnIndexOfTheArrayWhichIsSetToConfigurableFalseTestThatNewLengthIsSetToAValueGreaterThanTheNonDeletableIndexBy1WritableAttributeOfLengthIsSetToFalseAndTypeerrorExceptionIsThrown15451Step3IIii()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-168.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyAndAlsoLesserThanAnIndexOfTheArrayWhichIsSetToConfigurableFalseTestThatNewLengthIsSetToAValueGreaterThanTheNonDeletableIndexBy1AndTypeerrorIsThrown15451Step3LI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-169.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep10AIi1OfDefineownpropertyRejectsChangingTheValueOfNonWritableProperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyAndAlsoLesserThanAnIndexOfTheArrayWhichIsSetToConfigurableFalseTestThatNewLengthIsSetToAValueGreaterThanTheNonDeletableIndexBy1WritableAttributeOfLengthIsSetToFalseAndTypeerrorExceptionIsThrown15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-170.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfAnInheritedDataPropertyWithLargeIndexNamedInOCanTStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-171.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnDataPropertyWithLargeIndexNamedInOThatOverridesAnInheritedDataPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-172.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnDataPropertyWithLargeIndexNamedInOThatOverridesAnInheritedAccessorPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-173.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnAccessorPropertyWithLargeIndexNamedInOCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-174.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfAnInheritedAccessorPropertyWithLargeIndexNamedInOCanTStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-175.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnAccessorPropertyWithLargeIndexNamedInOThatOverridesAnInheritedDataPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-176.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnAccessorPropertyWithLargeIndexNamedInOThatOverridesAnInheritedAccessorPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-177.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableLargeIndexNamedPropertyOfOIsDeleted15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-178.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsGreaterThanValueOfTheLengthPropertyTestValueOfTheLengthPropertyIsSameAsValue15451Step3LIii1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-179-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep11AIOfDefineownpropertyRejectsChangingTheSetterIfPresent()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToFalseAtLastWhenTheWritableFieldOfDescIsFalseAndODoesnTContainNonConfigurableLargeIndexNamedProperty15451Step3M()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-181.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAvailableStringValuesThatConvertToNumbers15451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-182.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsBoundaryValue232215451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-183.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsBoundaryValue232115451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-184.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsBoundaryValue23215451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-185.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsBoundaryValue232115451Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-186.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTestTypeerrorIsNotThrownIfTheWritableAttributeOfTheLengthPropertyInOIsFalseAndValueOfNameIsLessThanValueOfTheLengthProperty15451Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-187.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTestTypeerrorIsThrownIfTheWritableAttributeOfTheLengthPropertyInOIsFalseAndValueOfNameEqualsToValueOfTheLengthProperty15451Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-188.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTestTypeerrorIsThrownIfTheWritableAttributeOfTheLengthPropertyInOIsFalseAndValueOfNameIsGreaterThanValueOfTheLengthProperty15451Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-189.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep11AIOfDefineownpropertyPermitsSettingASetterIfAbsent()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsOwnDataPropertyTestTypeerrorIsThrownOnUpdatingTheConfigurableAttributeFromFalseToTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-190.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAnInheritedDataPropertyTestThatDefiningOwnIndexNamedPropertyIsSuccessful15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-191.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsOwnDataPropertyThatOverridesAnInheritedDataPropertyTestTypeerrorIsThrownOnUpdatingTheConfigurableAttributeFromFalseToTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-192.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsOwnDataPropertyThatOverridesAnInheritedAccessorPropertyTestTypeerrorIsThrownWhenUpdateTheConfigurableAttributeToTrueAndValueOfConfigurableAttributeOfOriginalIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-193.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsOwnAccessorPropertyTestTypeerrorIsThrownOnUpdatingTheConfigurableAttributeFromFalseToTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-194.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAnInheritedAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-195.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-196.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-197.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOTestTypeerrorIsThrownWhenOIsNotExtensible15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-198.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOTestNameIsDefinedAsDataPropertyWhenDescIsGenericDescriptor15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-199.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNewlyDefinedDataPropertiesAttributesMissingFromDescShouldHaveValuesSetToTheDefaultsFrom861()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep11AIiOfDefineownpropertyRejectsChangingTheGetterIfPresent()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOTestValueOfNamePropertyOfAttributesIsSetAsUndefinedIfValueIsAbsentInDataDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-200.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOAndWritableIsAbsentInDataDescriptorDescTestWritableAttributeOfPropertyNameIsSetToFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-201.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOAndEnumerableIsAbsentInDataDescriptorDescTestEnumerableOfPropertyNameIsSetToFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-202.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOAndConfigurableIsAbsentInDataDescriptorDescTestConfigurableOfPropertyNameIsSetToFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-203.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyDescIsDataDescriptorTestUpdatingAllAttributeValuesOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-204.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOAndGetIsAbsentInAccessorDescriptorDescTestGetAttributeOfPropertyNameIsSetToUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-205.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOTestSetOfNamePropertyInAttributesIsSetAsUndefinedIfSetIsAbsentInAccessorDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-206.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOAndEnumerableIsAbsentInAccessorDescriptorDescTestEnumerableAttributeOfPropertyNameIsSetToFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-207.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNamePropertyDoesnTExistInOAndConfigurableIsAbsentInAccessorDescriptorDescTestConfigurableAttributeOfPropertyNameIsSetToFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-208.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyDescIsAccessorDescriptorTestUpdatingAllAttributeValuesOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-209.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNonConfigurablePropertiesStep11AIiOfDefineownpropertyPermitsSettingAGetterIfAbsent()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameMakesNoChangeIfEveryFieldInDescIsAbsentNameIsDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-210.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameMakesNoChangeIfEveryFieldInDescIsAbsentNameIsAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-211.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameMakesNoChangeIfTheValueOfEveryFieldInDescIsTheSameValueAsTheCorrespondingFieldInNameDescIsDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-212.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameMakesNoChangeIfTheValueOfEveryFieldInDescIsTheSameValueAsTheCorrespondingFieldInNameDescIsAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-213.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyAndItsConfigurableAndWritableAttributesAreSetToFalseTestTypeerrorIsThrownWhenTheTypeOfTheValueFieldOfDescIsDifferentFromTheTypeOfTheValueAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-214.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyBothTheValueFieldOfDescAndTheValueAttributeValueOfNameAreUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-215.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyBothTheValueFieldOfDescAndTheValueAttributeValueOfNameAreNull15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-216.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyBothTheValueFieldOfDescAndTheValueAttributeValueOfNameAreNan15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-217.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTestTypeerrorIsThrownWhenTheValueFieldOfDescIs0AndTheValueAttributeValueOfNameIs015451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-218.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTestTypeerrorIsThrownWhenTheValueFieldOfDescIs0AndTheValueAttributeValueOfNameIs015451Step4C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-219.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsExistingOwnDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoNumbersWithSameVaule15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-220.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTestTypeerrorIsThrownWhenTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoNumbersWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-221.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoStringsWhichHaveSameLengthAndSameCharactersInCorrespondingPositions15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-222.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTestTypeerrorIsThrownWhenTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoStringsWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-223.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoBooleansWithSameValue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-224.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTestTypeerrorIsThrownWhenTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoBooleansWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-225.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoObjectsWhichReferToTheSameObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-226.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTestTypeerrorIsThrownWhenTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoObjectsWhichReferToTwoDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-227.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheWritableFieldOfDescAndTheWritableAttributeValueOfNameAreTwoBooleansWithSameValue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-228.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheWritableFieldOfDescAndTheWritableAttributeValueOfNameAreTwoBooleansWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-229.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsExistingAnInheritedDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheGetFieldOfDescAndTheGetAttributeValueOfNameAreTwoObjectsWhichReferToTheSameObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-230.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheGetFieldOfDescAndTheGetAttributeValueOfNameAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-231.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheSetFieldOfDescAndTheSetAttributeValueOfNameAreTwoObjectsWhichReferToTheSameObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-232.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheSetFieldOfDescAndTheSetAttributeValueOfNameAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-233.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheEnumerableFieldOfDescAndTheEnumerableAttributeValueOfNameAreTwoBooleansWithSameValue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-234.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheEnumerableFieldOfDescAndTheEnumerableAttributeValueOfNameAreTwoBooleansWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-235.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheConfigurableFieldOfDescAndTheConfigurableAttributeValueOfNameAreTwoBooleansWithSameValue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-236.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexPropertyTheConfigurableFieldOfDescAndTheConfigurableAttributeValueOfNameAreTwoBooleansWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-237.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTypeerrorIsThrownIfTheConfigurableAttributeValueOfNameIsFalseAndTheConfigurableFieldOfDescIsTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-238.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTypeerrorIsThrownIfTheConfigurableAttributeValueOfNameIsFalseAndEnumerableOfDescIsPresentAndItsValueIsDifferentFromTheEnumerableAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-239.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsOwnDataPropertyThatOverridesAnInheritedDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTypeerrorIsThrownIfNameIsAccessorPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-240.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTypeerrorIsThrownIfNameIsDataPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-241.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsTrueTestNameIsUpdatedSuccessfully15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-242-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsTrueTestNameIsConvertedFromDataPropertyToAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-242.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndAssignmentToTheAccessorPropertyFailsToConvertAccessorPropertyFromAccessorPropertyToDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-243-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsTrueTestNameIsConvertedFromAccessorPropertyToDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-243.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheWritableFieldOfDescIsTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-244.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheTypeOfTheValueFieldOfDescIsDifferentFromTheTypeOfTheValueAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-245.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheValueFieldOfDescIs0AndTheValueAttributeValueOfNameIs015451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-246.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheValueFieldOfDescIs0AndTheValueAttributeValueOfNameIs015451Step4C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-247.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoNumbersWithDifferentVaules15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-248.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoStringsWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-249.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDataIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoBooleansWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-250.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfNameIsFalseAndTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-251.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheSetFieldOfDescIsPresentAndTheSetFieldOfDescAndTheSetAttributeValueOfNameAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-252.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheSetFieldOfDescIsPresentAndTheSetFieldOfDescIsAnObjectAndTheSetAttributeValueOfNameIsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-253.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsNotThrownIfTheSetFieldOfDescIsPresentAndTheSetFieldOfDescAndTheSetAttributeValueOfNameAreUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-254.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheGetFieldOfDescIsPresentAndTheGetFieldOfDescAndTheGetAttributeValueOfNameAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-255.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsThrownIfTheGetFieldOfDescIsPresentAndTheGetFieldOfDescIsAnObjectAndTheGetAttributeValueOfNameIsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-256.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfNameIsFalseTestTypeerrorIsNotThrownIfTheGetFieldOfDescIsPresentAndTheGetFieldOfDescAndTheGetAttributeValueOfNameAreUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-257.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheValueAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-258.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorTestSettingTheValueAttributeValueOfNameAsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-259.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsOwnAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorTestSettingTheValueAttributeValueOfNameFromUndefinedToNumber15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-260.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheWritableAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-261.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheEnumerableAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-262.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheConfigurableAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-263.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsDataPropertyAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-264.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheGetAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-265.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestSettingTheGetAttributeValueOfNameAsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-266.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheGetAttributeValueOfNameFromUndefinedToFunctionObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-267.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheSetAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-268.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestSettingTheSetAttributeValueOfNameAsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-269.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsAnInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheSetAttributeValueOfNameFromUndefinedToFunctionObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-270.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheEnumerableAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-271.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheConfigurableAttributeValueOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-272.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyNameIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfName15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-273.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTestTheLengthPropertyOfOIsNotChangedIfTouint32NameIsLessThanValueOfTheLengthPropertyInO15451Step4E()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-274.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTestTheLengthPropertyOfOIsSetAsTouint32Name1IfTouint32NameEqualsToValueOfTheLengthPropertyInO15451Step4EIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-275.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsAnArrayIndexNamedPropertyTestTheLengthPropertyOfOIsSetAsTouint32Name1IfTouint32NameIsGreaterThanValueOfTheLengthPropertyInO15451Step4EIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-276.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericPropertyThatWonTExistOnOAndDescIsDataDescriptorTestNameIsDefinedInOWithAllCorrectAttributeValues15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-277.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsGenericPropertyThatWonTExistOnOAndDescIsAccessorDescriptorTestNameIsDefinedInOWithAllCorrectAttributeValues15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-278.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnAccessorPropertyOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfName15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-279.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnDataPropertyOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfName15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-280.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfNameWhichIsDefinedAsNonWritableAndNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-281.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfNameWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-282.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-283.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-284.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfNameWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-285.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnAccessorPropertyOfOAndDescIsAccessorDescriptorTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfName15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-286.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-287.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayNameIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-288.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnPropertyWhichIsDefinedInBothParametermapOfOAndOAndIsDeletedAfterwardsAndDescIsDataDescriptorTestNameIsRedefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-289-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnPropertyOfOAndIsDeletedAfterwardsAndDescIsDataDescriptorTestNameIsRedefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-289.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnPropertyWhichIsDefinedInBothParametermapOfOAndOIsDeletedAfterwardsAndDescIsAccessorDescriptorTestNameIsRedefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-290-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnPropertyOfOAndIsDeletedAfterwardsAndDescIsAccessorDescriptorTestNameIsRedefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-290.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfName106DefineownpropertyStep3And5AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-291-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnAccessorPropertyOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfName106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-291.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnPropertyOfOWhichIsAlsoDefinedInParametermapOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfName106DefineownpropertyStep3And5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-292-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnDataPropertyOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfName106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-292.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnDataPropertyOfOTestTypeerrorIsNotThrownWhenUpdatingTheValueAttributeValueOfNameWhichIsDefinedAsNonWritableAndConfigurable106DefineownpropertyStep3And5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-293-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfNameWhichIsDefinedAsUnwritableAndNonConfigurable106DefineownpropertyStep4AndStep5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-293-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsNotThrownWhenUpdatingTheValueAttributeValueOfNameWhichIsDefinedAsNonWritableAndConfigurable106DefineownpropertyStep3AndStep5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-293-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfNameWhichIsDefinedAsNonWritableAndNonConfigurable106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-293.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4And5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-294-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-294.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4AndStep5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-295-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-295.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4AndStep5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-296-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-296.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4AndStep5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-297-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-297.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertySteps4And5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-298-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-298.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertySteps4And5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-299-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-299.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNewlyDefinedAccessorPropertiesAttributesMissingFromDescShouldHaveValuesSetToTheDefaultsFrom861()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsOwnAccessorPropertyWithoutAGetFunction8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4AndStep5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-300-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-300.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsAnIndexNamedPropertyOfOAndDescIsDataDescriptorTestNameIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-301-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnArrayIndexNamedPropertyOfOButNotDefinedInParametermapOfOAndDescIsDataDescriptorTestNameIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-301.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsAnIndexNamedPropertyOfOButNotDefinedInParametermapOfOAndDescIsAccessorDescriptorTestNameIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3AndStep5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-302-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedPropertyOfOButNotDefinedInParametermapOfOAndDescIsAccessorDescriptorTestNameIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-302.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfName106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-303.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfName106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-304.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfNameWhichIsNotWritableAndNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-305.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-306.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-307.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-308.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-309.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-310.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-311.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsAnIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-312.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnPropertyAndDescIsDataDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-313-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsGenericPropertyAndDescIsDataDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-313.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsPropertyAndDescIsAccessorDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-314-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsGenericPropertyAndDescIsAccessorDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-314.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnAccessorPropertyOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-315-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsGenericOwnAccessorPropertyOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-315.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnDataPropertyOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-316-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsGenericOwnDataPropertyOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-316.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfPWhichIsNotWritableAndNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-317-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfPWhichIsNotWritableAndNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-317.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersNameIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-318-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfNameWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-318.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-319-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-319.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsAnInheritedAccessorPropertyWithoutAGetFunction8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-320-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-320.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-321-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-321.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-322-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-322.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-323-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-323.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectOfAFunctionThatHasFormalParametersPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-324-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-324.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectWhichCreatedWithFunctionTakeFormalParametersNameIsOwnPropertyOfParametermapOfOTestNameIsDeletedIfNameIsConfigurableAndDescIsAccessorDescriptor106DefineownpropertyStep5AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-325-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectNameIsOwnPropertyOfParametermapOfOTestNameIsDeletedIfNameIsConfigurableAndDescIsAccessorDescriptor106DefineownpropertyStep5AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-325.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsTrueConfigurableIsTrueIsWritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-326.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsTrueConfigurableIsTrueIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-327.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsTrueConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-328.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateWritableAttributeOfDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-329.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAFunctionObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-330.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-331.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsTrueToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-332.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfNamedPropertyPSuccessfullyWhenConfigurableAttributeIsFalseWritableAttributeIsTrueAndOIsAnObjectObject8129Step10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesIndexedDataPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseIsWritableUsingSimpleAssignmentOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesIndexedPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseIsWritableUsingSimpleAssignmentOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfIndexedPropertyPSuccessfullyWhenConfigurableAttributeIsFalseWritableAttributeIsTrueAndAIsAnArrayObject8129Step10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfNamedPropertyPSuccessfullyWhenConfigurableAttributeIsFalseWritableAttributeIsTrueAndOIsAnArgumentsObject8129Step10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void IndexedPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseIsWritableUsingSimpleAssignmentAIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesNamedDataPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseIsWritableUsingSimpleAssignmentOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfIndexedPropertyPSuccessfullyWhenConfigurableAttributeIsFalseWritableAttributeIsTrueAndOIsAnObjectObject8129Step10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfNamedPropertyPSuccessfullyWhenConfigurableAttributeIsFalseWritableAttributeIsTrueAndAIsAnArrayObject8129Step10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfIndexedPropertyPSuccessfullyWhenConfigurableAttributeIsFalseWritableAttributeIsTrueAndOIsAnArgumentsObject8129Step10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamedPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseIsWritableUsingSimpleAssignmentAIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsTrueConfigurableIsFalseIsWritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsTrueConfigurableIsFalseIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-334.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsTrueConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-335.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateWritableAttributeOfDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-336.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-337.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-338.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdatingIndexedDataPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseToAnAccessorPropertyDoesNotSucceedAIsAnArrayObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-339-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdatingNamedDataPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseToAnAccessorPropertyDoesNotSucceedOIsAnArgumentsObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-339-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdatingNamedDataPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseToAnAccessorPropertyDoesNotSucceedAIsAnArrayObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-339-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdatingIndexedDataPropertyPWithAttributesWritableTrueEnumerableTrueConfigurableFalseToAnAccessorPropertyDoesNotSucceedOIsAnArgumentsObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-339-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheDataPropertyWritableIsTrueEnumerableIsTrueConfigurableIsFalseToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-339.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArrayObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsFalseConfigurableIsTrueIsWritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-340.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsFalseConfigurableIsTrueIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-341.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsFalseConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-342.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateWritableAttributeOfDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-343.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-344.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-345.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsTrueToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-346.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsFalseConfigurableIsFalseIsWritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-347.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsFalseConfigurableIsFalseIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-348.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsTrueEnumerableIsFalseConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-349.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAStringObjectWhichImplementsItsOwnGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateWritableAttributeOfDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-350.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-351.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-352.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheDataPropertyWritableIsTrueEnumerableIsFalseConfigurableIsFalseToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-353.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfNamedPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseOIsAnObjectObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfIndexedPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseOIsAnObjectObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfNamedPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseAIsAnArrayObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfIndexedPropertySuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseOIsAnArgumentsObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfIndexedPropertySuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseOIsTheGlobalObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamedPropertyPWithAttributesWritableFalseEnumerableTrueConfigurableTrueIsNonWritableUsingSimpleAssignmentAIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyPIsAnIndexedDataPropertyWithAttributesWritableFalseEnumerableTrueConfigurableTrueIsNonWritableUsingSimpleAssignmentOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeOfIndexedPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseAIsAnArrayObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseOIsAnArgumentsObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateValueAttributeSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseOIsTheGlobalObject8129StepNote()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyIndexedPropertyPWithAttributesWritableFalseEnumerableTrueConfigurableTrueIsNonWritableUsingSimpleAssignmentAIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyPWithAttributesWritableFalseEnumerableTrueConfigurableTrueIsNonWritableUsingSimpleAssignmentOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyPWithAttributesWritableFalseEnumerableTrueConfigurableTrueIsNonWritableUsingSimpleAssignmentOIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsTrueConfigurableIsTrueIsUnwritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsTrueConfigurableIsTrueIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-355.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsTrueConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-356.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateWritableAttributeOfDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-357.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-358.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-359.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsABooleanObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingIndexedDataPropertyPWhoseAttributesAreWritableFalseEnumerableTrueConfigurableTrueToAnAccessorPropertyAIsAnArrayObject8129Step9BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-360-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingDataPropertyPWhoseAttributesAreWritableFalseEnumerableTrueConfigurableTrueToAnAccessorPropertyOIsAnArgumentsObject8129Step9BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-360-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingDataPropertyPWhoseAttributesAreWritableFalseEnumerableTrueConfigurableTrueToAnAccessorPropertyOIsTheGlobalObject8129Step9BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-360-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingNamedDataPropertyPWhoseAttributesAreWritableFalseEnumerableTrueConfigurableTrueToAnAccessorPropertyAIsAnArrayObject8129Step9BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-360-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingIndexedDataPropertyPWhoseAttributesAreWritableFalseEnumerableTrueConfigurableTrueToAnAccessorPropertyOIsAnArgumentsObject8129Step9BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-360-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingIndexedDataPropertyPWhoseAttributesAreWritableFalseEnumerableTrueConfigurableTrueToAnAccessorPropertyOIsTheGlobalObject8129Step9BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-360-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsTrueToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-360.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsTrueConfigurableIsFalseIsUnwritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-361.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsTrueConfigurableIsFalseIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-362.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsTrueConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-363.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateWritableAttributeOfDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-364.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-365.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-366.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheDataPropertyWritableIsFalseEnumerableIsTrueConfigurableIsFalseToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-367.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsFalseConfigurableIsTrueIsUnwritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-368.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsFalseConfigurableIsTrueIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-369.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsANumberObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsFalseConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-370.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateWritableAttributeOfDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-371.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-372.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-373.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsTrueToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-374.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsFalseConfigurableIsFalseIsUnwritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-375.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsFalseConfigurableIsFalseIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-376.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyWritableIsFalseEnumerableIsFalseConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-377.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateWritableAttributeOfDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-378.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-379.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsTheMathObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-380.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheDataPropertyWritableIsFalseEnumerableIsFalseConfigurableIsFalseToAnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-381.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsANumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-382.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-383.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-384.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsAGenericObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-385.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-386.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsAStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-387.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsANumberObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-388.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-389.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsADateObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-390.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-391.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsADateObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-392.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-393.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-394.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-395.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsNan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-396.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-397.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-398.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfDataPropertyIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-399.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyForNewlyDefinedPropertiesStep4A1OfDefineownpropertyCreatesADataPropertyIfHandedAGenericDesc()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsARegexpObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectStringInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-402.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessfullyAddAPropertyToAnObjectWhenTheObjectSPrototypeHasAPropertyWithSameNameAndWritableAttributeIsSetToTrueArrayInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-403.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyWhoseEnumerableAttributeIsSetToTrueIsEnumerableBooleanInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-404.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailedToAddAPropertyToAnObjectWhenTheObjectSObjectHasAPropertyWithSameNameAndWritableAttributeIsSetToFalseNumberInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-405.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyWhoseEnumerableAttributeIsSetToFalseIsNonEnumerableFunctionInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-406.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectErrorInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-407.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessfullyAddAPropertyToAnObjectWhenTheObjectSPrototypeHasAPropertyWithSameNameAndWritableAttributeIsSetToTrueDateInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-408.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyWhoseEnumerableAttributeIsSetToFalseIsEnumerableRegexpInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-409.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsTheJsonObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailedToAddAPropertyToAnObjectWhenTheObjectSPrototypeHasAPropertyWithTheSameNameAndWritableSetToFalseJson()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-410.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyWhoseEnumerableAttributeIsSetToFalseIsNonEnumerableMath()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-411.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueFieldOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-412.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessfullyAddAPropertyToAnObjectWhenTheObjectSPrototypeHasAPropertyWithTheSameNameAndWritableSetToTrueObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-413.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyWhoseEnumerableAttributeIsSetToTrueIsEnumerableObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-414.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailedToAddPropertiesToAnObjectWhenTheObjectSPrototypeHasPropertiesWithTheSameNameAndWritableSetToFalseObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-415.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertiesWhoseEnumerableAttributeIsSetToFalseIsNonEnumerableObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-416.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesValueAttributeOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-417.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessfullyAddAPropertyToAnObjectWhenTheObjectSPrototypeHasAPropertyWithTheSameNameAndWritableSetToTrueFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-418.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyWhoseEnumerableAttributeIsSetToTrueIsEnumerableFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-419.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnErrorObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailedToAddAPropertyToAnObjectWhenTheObjectSPrototypeHasAPropertyWithTheSameNameAndWritableSetToFalseFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-420.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyWhoseEnumerableAttributeIsSetToFalseIsNonEnumerableFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-421.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-422.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-423.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-424.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-425.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-426.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-427.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-428.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-429.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsAnArgumentsObjectWhichImplementsItsOwnGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-430.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-431.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-432.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-433.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-434.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-435.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-436.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-437.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-438.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-439.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-440.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-441.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-442.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-443.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-444.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-445.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-446.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-447.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-448.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-449.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyOIsTheGlobalObjectThatUsesObjectSGetownpropertyMethodToAccessTheNameProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-450.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-451.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-452.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-453.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-454.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-455.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-456.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsUndefinedSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-457.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-458.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-459.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsDefinedAsDataPropertyIfNamePropertyDoesnTExistInOAndDescIsGenericDescriptor8129Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-460.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-461.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-462.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-463.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-464.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-465.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-466.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-467.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-468.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-469.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOValueOfNamePropertyIsSetAsUndefinedIfItIsAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-470.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-471.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-472.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-473.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-474.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-475.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-476.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-477.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-478.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-479.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOTestWritableOfNamePropertyOfAttributesIsSetAsFalseValueIfAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-480.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-481.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-482.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-483.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-484.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-485.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-486.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-487.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-488.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-489.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOTestEnumerableOfNamePropertyOfAttributesIsSetAsFalseValueIfAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-490.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-491.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-492.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsUndefinedSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-493.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-494.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-495.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-496.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-497.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-498.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-499.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyStep6OfDefineownpropertyReturnsIfEveryFieldOfDescAlsoOccursInCurrentAndEveryFieldInDescHasTheSameValueAsCurrent()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOTestConfigurableOfNamePropertyIsSetAsFalseIfItIsAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-500.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-501.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-502.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-503.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-504.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-505.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-506.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-507.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-508.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-509.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescIsDataDescriptorTestUpdatingAllAttributeValuesOfName8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-510.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsTrueConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-511.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-512.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-513.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-514.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-515.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-516.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-517.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-518.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-519.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescIsGenericDescriptorWithoutAnyAttributeTestNameIsDefinedInObjWithAllDefaultAttributeValues8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-52.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-520.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-521.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-522.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-523.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-524.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-525.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-526.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-527.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-528.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsAFunctionSetIsUndefinedEnumerableIsFalseConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-529.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOTestGetOfNamePropertyIsSetAsUndefinedIfItIsAbsentInAccessorDescriptorDesc8129Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-53.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-530.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfNamedAccessorPropertyPSuccessfullyWhenConfigurableAttributeIsTrueOIsAnObjectObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfIndexedAccessorPropertyPSuccessfullyWhenConfigurableAttributeIsTrueOIsAnObjectObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfNamedAccessorPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAIsAnArrayObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfIndexedAccessorPropertySuccessfullyWhenConfigurableAttributeIsTrueOIsAnArgumentsObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfIndexedAccessorPropertyPSuccessfullyWhenConfigurableAttributeIsTrueOIsTheGlobalObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPUsingSimpleAssignmentIsSuccessfulAIsAnArrayObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPUsingSimpleAssignmentOIsAnArgumentsObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPUsingSimpleAssignmentIsSuccessfulOIsTheGlobalObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfIndexedAccessorPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAIsAnArrayObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfNamedAccessorPropertySuccessfullyWhenConfigurableAttributeIsTrueOIsAnArgumentsObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillUpdateGetAndSetAttributesOfNamedAccessorPropertyPSuccessfullyWhenConfigurableAttributeIsTrueOIsTheGlobalObject8129Step11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPWithoutSetUsingSimpleAssignmentIsFailedAIsAnArrayObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPWithoutSetUsingSimpleAssignmentIsFailedOIsAnArgumentsObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPWithoutSetUsingSimpleAssignmentIsFailedOIsTheGlobalObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-532.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-533.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-534.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-535.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-536.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-537.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedPWhoseConfigurableAttributeIsTrueToADataPropertyIsSuccessfulAIsAnArrayObject8129Step9CI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-538-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPWhoseConfigurableAttributeIsTrueToADataPropertyIsSuccessfulOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-538-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPWhoseConfigurableAttributeIsTrueToADataPropertyIsSuccessfulOIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-538-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPWhoseConfigurableAttributeIsTrueToADataPropertyIsSuccessfulAIsAnArrayObject8129Step9CI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-538-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPWhoseConfigurableAttributeIsTrueToADataPropertyIsSuccessfulOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-538-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPWhoseConfigurableAttributeIsTrueToADataPropertyIsSuccessfulOIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-538-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-538.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-539.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOTestSetOfNamePropertyOfAttributesIsSetAsUndefinedValueIfAbsentInAccessorDescriptorDesc8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-54.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyFailsToUpdateGetAndSetAttributesOfANamedAccessorPropertyPWhoseConfigurableAttributeIsFalseAndThrowsTypeerrorExceptionOIsAnObjectObject8129Step11A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPUsingSimpleAssignmentIsSuccessfulOIsAnArgumentsObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyFailsToUpdateGetAndSetAttributesOfAnIndexedPropertyPWhoseConfigurableAttributeIsFalseAndThrowsTypeerrorExceptionAIsAnArrayObject8129Step11A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyFailsToUpdateGetAndSetAttributesOfANamedAccessorPropertyPWhoseConfigurableAttributeIsFalseOIsAnArgumentsObject8129Step11A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPUsingSimpleAssignmentIsSuccessfulAIsAnArrayObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPUsingSimpleAssignmentIsSuccessfulOIsAnArgumentsObject8125Step5B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyFailsToUpdateGetAndSetAttributesOfAnIndexedAccessorPropertyPWhoseConfigurableAttributeIsFalseAndThrowsTypeerrorExceptionOIsAnObjectObject8129Step11A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyFailsToUpdateGetAndSetAttributesOfANamedPropertyPWhoseConfigurableAttributeIsFalseAndThrowsTypeerrorExceptionAIsAnArrayObject8129Step11A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyFailsToUpdateGetAndSetAttributesOfAnIndexedAccessorPropertyPWhoseConfigurableAttributeIsFalseOIsAnArgumentsObject8129Step11A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPUsingSimpleAssignmentIsSuccessfulAIsAnArrayObject8125Step5B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-540.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-541.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-542.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-543.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-544.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-545.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-546.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPWhoseConfigurableAttributeIsFalseToADataPropertyDoesNotSucceedAIsAnArrayObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-547-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPWhoseConfigurableAttributeIsFalseToADataPropertyDoesNotSucceedAIsAnArgumentsObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-547-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingANamedAccessorPropertyPWhoseConfigurableAttributeIsFalseToADataPropertyDoesNotSucceedAIsAnArrayObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-547-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesUpdatingAnIndexedAccessorPropertyPWhoseConfigurableAttributeIsFalseToADataPropertyDoesNotSucceedAIsAnArgumentsObject8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-547-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsTrueConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-547.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-548.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-549.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOTestEnumerableOfNamePropertyOfAttributesIsSetAsFalseValueIfAbsentInAccessorDescriptorDesc8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-55.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-550.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueIsDeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-551.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-552.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-553.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateEnumerableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-554.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-555.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateTheAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsTrueToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-556.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-557.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsTheExpectedFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-558.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsNonEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-559.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNamePropertyDoesnTExistInOTestConfigurableOfNamePropertyIsSetAsFalseIfItIsAbsentInAccessorDescriptorDesc8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-56.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseIsUndeletable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-560.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateGetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-561.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateSetAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-562.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateEnumerableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-563.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateConfigurableAttributeOfAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToDifferentValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-564.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateTheAccessorPropertyGetIsAFunctionSetIsAFunctionEnumerableIsFalseConfigurableIsFalseToADataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-565.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeIsAFunctionWhichHasZeroArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-566.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeIsAFunctionWhichHasOneArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-567.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeIsAFunctionWhichHasTwoArguments()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-568.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeIsAFunctionWhichContainsGlobalVariable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-569.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescIsAccessorDescriptorTestUpdatingAllAttributeValuesOfName8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-57.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeIsAFunctionWhichDoesnTContainsReturnStatement()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-570.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetAttributeIsAFunctionWhichInvolvesThisObjectIntoStatementS()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-571.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeIsAFunctionWhichHasZeroArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-572.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeIsAFunctionWhichHasOneArgument()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-573.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeIsAFunctionWhichHasTwoArguments()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-574.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeIsAFunctionWhichContainsGlobalVariable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-575.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeIsAFunctionWhichContainsReturnStatement()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-576.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSetAttributeIsAFunctionWhichInvolvesThisObjectIntoStatementS()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-577.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetFieldOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectStringInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-578.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToAddPropertyIntoObjectArrayInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-579.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsDataDescriptorAndEveryFieldsInDescIsAbsent8129Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-58.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsEnumerableBooleanInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-580.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToAddPropertyIntoObjectNumberInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-581.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsNonEnumerableFunctionInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-582.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetFieldOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectErrorInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-583.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailedToAddPropertyIntoObjectDateInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-584.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsEnumerableRegexpInstance()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-585.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateValueOfPropertyIntoOfProptotypeInternalPropertyJson()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-586.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsNonEnumerableMath()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-587.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetFieldOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-588.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateValueOfPropertyIntoOfProptotypeInternalPropertyObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-589.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyNameIsAccessorDescriptorAndEveryFieldsInDescIsAbsent8129Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-59.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsEnumerableObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-590.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateValueOfPropertyOfProptotypeInternalPropertyObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-591.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsNonEnumerableObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-592.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesGetFieldOfInheritedPropertyOfPrototypeInternalPropertyIsCorrectFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-593.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesSuccessToUpdateValueOfPropertyIntoOfProptotypeInternalPropertyFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-594.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsEnumerableFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-595.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesFailToUpdateValueOfPropertyIntoOfProptotypeInternalPropertyFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-596.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesInheritedPropertyIsNonEnumerableFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-597.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectGetprototypeofAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-598.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectGetownpropertydescriptorAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-599.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyStep6OfDefineownpropertyReturnsIfEveryFieldOfDescAlsoOccursInCurrentAndEveryFieldInDescHasTheSameValueAsCurrent2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyTypeOfDescValueIsDifferentFromTypeOfNameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-60.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectGetownpropertynamesAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-600.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectCreateAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-601.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectDefinepropertyAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-602.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectDefinepropertiesAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-603.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectSealAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-604.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectFreezeAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-605.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectPreventextensionsAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-606.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectIssealedAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-607.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectIsfrozenAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-608.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectIsextensibleAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-609.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescValueAndNameValueAreUndefined8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-61.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectKeysAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-610.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInFunctionPrototypeBindAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-611.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeIndexofAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-612.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInObjectLastindexofAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-613.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeEveryAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-614.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeSomeAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-615.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeForeachAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-616.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeMapAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-617.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeFilterAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-618.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeReduceAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-619.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescValueAndNameValueAreNull8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-62.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInArrayPrototypeReducerightAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-620.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInStringPrototypeTrimAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-621.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInDateNowAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-622.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInDatePrototypeToisostringAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-623.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Es5AttributesAllAttributesInDatePrototypeTojsonAreCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-624.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void GloballyDeclaredVariableShouldTakePrecedenceOverObjectPrototypePropertyOfTheSameName()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-625gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescValueAndNameValueAreNan8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-63.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescValue0AndNameValue08129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-64.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescValue0AndNameValue08129Step62()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-65.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescValueAndNameValueAreTwoNumbersWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-66.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescValueAndNameValueAreTwoStringsWhichHaveSameLengthAndSameCharactersInCorrespondingPositions8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-67.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescValueAndNameValueAreTwoStringsWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-68.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescValueAndNameValueAreBooleanValuesWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-69.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyStep7AOfDefineownpropertyRejectsIfCurrentConfigurableIsFalseAndDescConfigurableIsTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescValueAndNameValueAreTwoBooleanValuesWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-70.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescValueAndNameValueAreOjbectsWhichReferToTheSameObject8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-71.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescValueAndNameValueAreTwoOjbectsWhichReferToTheDifferentObjects8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-72.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescWritableAndNameWritableAreBooleanValuesWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-73.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescWritableAndNameWritableAreTwoBooleanValuesWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-74.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescGetAndNameGetAreTwoObjectsWhichReferToTheSameObject8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-75.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescGetAndNameGetAreTwoObjectsWhichReferToTheDifferentObjects8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-76.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescSetAndNameSetAreTwoObjectsWhichReferToTheSameObject8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-77.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescSetAndNameSetAreTwoObjectsWhichReferToTheDifferentObjects8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-78.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescEnumerableAndNameEnumerableAreBooleanValuesWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-79.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyStep7BOfDefineownpropertyRejectsIfCurrentEnumerableAndDescEnumerableAreTheBooleanNegationsOfEachOther3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescEnumerableAndNameEnumerableAreBooleanNegationOfEachOther8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-80.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyBothDescConfigurableAndNameConfigurableAreBooleansWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-81.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsFalseNamePropertyIsADataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsTrueAndConfigurableAttributeIsFalseNamePropertyIsAnAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAndConfigurableAttributesOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAndConfigurableAttributesAsFalseNamePropertyIsAnAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributesOfNamePropertyToTrueSuccessfullyWhenEnumerableAttributeOfNameIsFalseAndConfigurableAttributeOfNameIsTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsTrueNamePropertyIsAnAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsFalseNamePropertyIsAnIndexDataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsFalseAndConfigurableAttributeAsTrueNamePropertyIsAnIndexDataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenConfigurableAttributeOfNamePropertyIsTrueTheDescIsAGenericDescriptorWhichContainsConfigurableAttributeAsFalseNamePropertyIsAnIndexDataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsTrueAndConfigurableAttributeAsFalseNamePropertyIsAnIndexDataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAndConfigurableAttributesOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAndConfigurableAttributesAsFalseNamePropertyIsAnIndexDataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToTrueSuccessfullyWhenEnumerableAttributeOfNameIsFalseAndConfigurableAttributeOfNameIsTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsTrueNamePropertyIsAnIndexDataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsFalseAndNamePropertyIsAnIndexAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsFalseAndConfigurableAttributeAsTrueNamePropertyIsADataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsFalseAndConfigurablePropertyIsTrueNamePropertyIsAnIndexAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichOnlyContainsConfigurableAttributeAsFalseNamePropertyIsAnIndexAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsTrueAndConfigurableAttributeIsFalseNamePropertyIsAnIndexAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAndConfigurableAttributesOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAndConfigurableAttributesAsFalseNamePropertyIsAnIndexAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributesOfNamePropertyToTrueSuccessfullyWhenEnumerableAttributeOfNameIsFalseAndConfigurableAttributeOfNameIsTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsTrueNamePropertyIsAnIndexAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenConfigurableAttributeOfNamePropertyIsTrueTheDescIsAGenericDescriptorWhichContainsConfigurableAttributeAsFalseNamePropertyIsADataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsTrueAndConfigurableAttributeAsFalseNamePropertyIsADataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAndConfigurableAttributesOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAndConfigurableAttributesAsFalseNamePropertyIsADataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToTrueSuccessfullyWhenEnumerableAttributeOfNameIsFalseAndConfigurableAttributeOfNameIsTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsTrueNamePropertyIsADataProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichOnlyContainsEnumerableAttributeAsFalseAndNamePropertyIsAnAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateEnumerableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichContainsEnumerableAttributeAsFalseAndConfigurablePropertyIsTrueNamePropertyIsAnAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyUpdateConfigurableAttributeOfNamePropertyToFalseSuccessfullyWhenEnumerableAndConfigurableAttributesOfNamePropertyAreTrueTheDescIsAGenericDescriptorWhichOnlyContainsConfigurableAttributeAsFalseNamePropertyIsAnAccessorProperty8129Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyDescConfigurableAndNameConfigurableAreBooleanNegationOfEachOther8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-82.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorIfNameConfigurableFalseNameWritableFalseNameValueUndefinedAndDescValueUndefined8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-83.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorIfNameConfigurableFalseNameWritableFalseNameValueNullAndDescValueNull8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-84.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorIfNameConfigurableFalseNameWritableFalseNameValueNanAndDescValueNan8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-85.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValue0AndNameValue08129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-86.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValue0AndNameValue08129Step10AIi12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-87.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoNumbersWithTheSameValue8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-88.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoNumbersWithDifferentValues8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-89.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void Step4OfDefinepropertyCallsTheDefineownpropertyInternalMethodOfOToDefineThePropertyStep7BOfDefineownpropertyRejectsIfCurrentEnumerableAndDescEnumerableAreTheBooleanNegationsOfEachOther4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoStringsWithTheSameValue8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-90.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoStringsWithDifferentValues8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-91.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoBooleansWithTheSameValue8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-92.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoBooleansWithDifferentValues8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-93.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoObjectsReferToTheSameObject8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-94.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameWritableFalseDescValueAndNameValueAreTwoObjectsWhichReferToTheDifferentObjects8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-95.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorWhenNameConfigurableFalseBothDescSetAndNameSetAreTwoObjectsWhichReferToTheSameObject8129Step11AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-96.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameSetIsUndefinedDescSetRefersToAnObject8129Step11AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-97.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillNotThrowTypeerrorWhenNameConfigurableFalseBothDescGetAndNameGetAreTwoObjectsWhichReferToTheSameObject8129Step11AIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-98.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ObjectDefinepropertyWillThrowTypeerrorWhenNameConfigurableFalseNameGetIsUndefinedDescGetRefersToAnObject8129Step11AIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-99.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void IfAParticularApiExistsDocumentCreateelementAsHappensToExistInABrowserEnvironmentCheckIfTheFormObjectsItMakesObeyTheConstraintsThatEvenHostObjectsMustObeyInThisCaseThatIfDefinepropertySeemsToHaveSuccessfullyInstalledANonConfigurableGetterThatItIsStillThere()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/S15.2.3.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.6")]
        public void ChecksIfAnInheritedAccessorPropertyAppearsToBeAnOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.6/S15.2.3.6_A2.js", false);
        }


    }
}
