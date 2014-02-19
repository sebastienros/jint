using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesMustExistAsAFunctionTaking2Parameters()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorIfOIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorIfOIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorIfOIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorIfOIsAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorIfPropertiesIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsADateObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorIfPropertiesIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsABooleanWhoseValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsABooleanObjectWhosePrimitiveValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsAnyInterestingNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsANumberObjectWhosePrimitiveValueIsAnyInterestingNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsAStringWhoseValueIsAnyInterestingString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsAStringObjectWhosePrimitiveValueIsAnyInterestingString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesArgumentPropertiesIsAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableOwnDataPropertyOfPropertiesIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOwnDataPropertyOfPropertiesWhichIsNotEnumerableIsNotDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableInheritedDataPropertyOfPropertiesIsNotDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableOwnAccessorPropertyOfPropertiesIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOwnAccessorPropertyOfPropertiesWhichIsNotEnumerableIsNotDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableInheritedAccessorPropertyOfPropertiesIsNotDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesNoAdditionalPropertyIsDefinedInOWhenPropertiesDoesnTContainEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesNoAdditionalPropertyIsDefinedInOWhenPropertiesDoesnTContainEnumerableOwnProperty2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnDataPropertyThatOverridesEnumerableInheritedDataPropertyOfPropertiesIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsABooleanObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsANumberObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsTheMathObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsADateObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsARegexpObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsTheJsonObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsAnErrorObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsTheArgumentsObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnDataPropertyThatOverridesEnumerableInheritedAccessorPropertyOfPropertiesIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableOwnAccessorPropertyOfPropertiesThatOverridesEnumerableInheritedDataPropertyOfPropertiesIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableOwnAccessorPropertyOfPropertiesThatOverridesEnumerableInheritedAccessorPropertyOfPropertiesIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableOwnAccessorPropertyOfPropertiesWithoutAGetFunctionIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerableOwnAccessorPropertyOfPropertiesWithoutAGetFunctionThatOverridesEnumerableInheritedAccessorPropertyOfPropertiesIsDefinedInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsAnArrayObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPropertiesIsAStringObjectWhichImplementsItsOwnGetMethodToGetEnumerableOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsUndefined8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsBooleanObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-100.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsNumberObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-101.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsTheMathObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-102.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsDateObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-103.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsRegexpObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-104.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsTheJsonObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-105.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsErrorObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-106.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsTheArgumentObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-107.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsTheGlobalObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-109.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsAStringValueIsFalseWhichIsTreatedAsTrueValue8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-110.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsNewBooleanFalseWhichIsTreatedAsTrueValue8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-111.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsPresent8105Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-112.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsNotPresent8105Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-113.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-114.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-115.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-116.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-117.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-118.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-119.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-120.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-121.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunction8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-122.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-123.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValuePropertyOfDescobjIsInheritedAccessorPropertyWithoutAGetFunction8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-124.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAFunctionObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-125.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnArrayObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-126.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAStringObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-127.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsABooleanObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-128.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsANumberObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-129.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheMathObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-130.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsADateObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-131.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsARegexpObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-132.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheJsonObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-133.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnErrorObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-134.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheArgumentsObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-135.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheGlobalObjectWhichImplementsItsOwnGetMethodToGetValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-137.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsPresent8105Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-138.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsNotPresent8105Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-139.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-140.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-141.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-142.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-143.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-144.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-145.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-146.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-147.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunction8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-148.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-149.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWritablePropertyOfDescobjIsInheritedAccessorPropertyWithoutAGetFunction8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-150.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAFunctionObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-151.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnArrayObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-152.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAStringObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-153.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsABooleanObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-154.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsANumberObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-155.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheMathObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-156.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsADateObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-157.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsARegexpObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-158.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheJsonObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-159.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunction8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnErrorObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-160.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheArgumentsObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-161.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheGlobalObjectWhichImplementsItsOwnGetMethodToGetWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-163.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsUndefined8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-164.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsNull8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-165.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsTrue8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-166.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsFalse8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-167.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIs08105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-168.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIs08105Step6B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-169.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIs08105Step6B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-170.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsNan8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-171.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsPositiveNumber8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-172.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsNegativeNumber8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-173.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsEmptyString8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-174.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsNonEmptyString8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-175.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsFunctionObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-176.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsArrayObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-177.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsStringObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-178.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsBooleanObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-179.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsInheritedAccessorPropertyWithoutAGetFunction8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsNumberObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-180.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsTheMathObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-181.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsDateObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-182.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsRegexpObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-183.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsTheJsonObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-184.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsErrorObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-185.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsTheArgumentObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-186.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsTheGlobalObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-188.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsAStringValueIsFalseWhichIsTreatedAsTrueValue8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-189.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAFunctionObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfWritablePropertyOfDescobjIsNewBooleanFalseWhichIsTreatedAsTrueValue8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-190.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsPresent8105Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-191.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsNotPresent8105Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-192.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-193.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-194.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-195.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-196.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-197.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-198.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-199.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsNull8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnArrayObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-200.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunction8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-201.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-202.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesGetPropertyOfDescobjIsInheritedAccessorPropertyWithoutAGetFunction8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-203.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAFunctionObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-204.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnArrayObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-205.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAStringObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-206.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsABooleanObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-207.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsANumberObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-208.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheMathObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-209.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAStringObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsADateObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-210.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsARegexpObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-211.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheJsonObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-212.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnErrorObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-213.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheArgumentsObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-214.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheGlobalObjectWhichImplementsItsOwnGetMethodToGetGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-216.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfGetPropertyOfDescobjIsUndefined8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-217.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfGetPropertyOfDescobjIsPrimitiveValuesValueIsNull8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-218.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfGetPropertyOfDescobjIsPrimitiveValuesValueIsBoolean8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-219.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsABooleanObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfGetPropertyOfDescobjIsPrimitiveValuesValueIsNumber8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-220.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfGetPropertyOfDescobjIsPrimitiveValuesValueIsString8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-221.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfGetPropertyOfDescobjIsAppliedToArrayObject8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-222.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfGetPropertyOfDescobjIsAFunction8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-223.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsPresent8105Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-226.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsNotPresent8105Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-227.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-228.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-229.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsANumberObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-230.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-231.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-232.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-233.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-234.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-235.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunction8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-236.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-237.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesSetPropertyOfDescobjIsInheritedAccessorPropertyWithoutAGetFunction8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-238.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAFunctionObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-239.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheMathObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnArrayObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-240.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAStringObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-241.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsABooleanObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-242.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsANumberObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-243.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheMathObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-244.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsADateObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-245.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsARegexpObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-246.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheJsonObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-247.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnErrorObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-248.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheArgumentsObjectWhichImplementsItsOwnGetMethodToGetSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-249.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsADateObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfSetPropertyOfDescobjIsUndefined8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-252.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfSetPropertyOfDescobjIsPrimitiveValuesNull8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-253.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfSetPropertyOfDescobjIsPrimitiveValuesBoolean8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-254.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfSetPropertyOfDescobjIsPrimitiveValuesNumber8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-255.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfSetPropertyOfDescobjIsPrimitiveValuesString8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-256.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfSetPropertyOfDescobjIsAnInterestingObjectOtherThanAFunction8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-257.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfSetPropertyOfDescobjIsAFunction8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-258.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsARegexpObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfBothSetPropertyAndValuePropertyOfDescobjArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-261.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfBothSetPropertyAndWritablePropertyOfDescobjArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-262.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfBothGetPropertyAndValuePropertyOfDescobjArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-263.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfBothGetPropertyAndWritablePropertyOfDescobjArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-264.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheJsonObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnErrorObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheArgumentsObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsABoolean8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheGlobalObjectWhichImplementsItsOwnGetMethodToGetEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsUndefined8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsNull8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsTrue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsFalse8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIs08105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIs08105Step3B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIs08105Step3B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsNan8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsANumber8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsPositiveNumber8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsNegativeNumber8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsEmptyString8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsNonEmptyString8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsAFunctionObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsAnArrayObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsAStringObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsABooleanObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsANumberObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsTheMathObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAString8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsADateObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsARegexpObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsTheJsonObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-52.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsAnErrorObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-53.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsTheArgumentsObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-54.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsTheGlobalObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-56.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsAStringValueIsFalseWhichIsTreatedAsTrueValue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-57.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfEnumerablePropertyOfDescobjIsNewBooleanFalseWhichIsTreatedAsTrueValue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-58.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsPresent8105Step4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-59.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsPresent8105Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsNotPresent8105Step4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-60.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-61.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-62.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-63.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-64.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-65.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-66.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-67.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-68.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunction8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-69.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsNotPresent8105Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-70.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesConfigurablePropertyOfDescobjIsInheritedAccessorPropertyWithoutAGetFunction8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-71.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAFunctionObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-72.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnArrayObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-73.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAStringObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-74.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsABooleanObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-75.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsANumberObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-76.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheMathObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-77.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsADateObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-78.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsARegexpObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-79.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsOwnDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheJsonObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-80.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsAnErrorObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-81.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheArgumentsObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-82.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescobjIsTheGlobalObjectWhichImplementsItsOwnGetMethodToGetConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-84.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsUndefined8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-85.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsNull8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-86.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsTrue8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-87.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsFalse8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-88.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIs08105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-89.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEnumerablePropertyOfDescobjIsInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIs08105Step4B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-90.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIs08105Step4B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-91.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsNan8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-92.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsPositiveNumber8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-93.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsNegativeNumber8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-94.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsEmptyString8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-95.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsNonEmptyString8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-96.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsFunctionObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-97.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsArrayObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-98.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesValueOfConfigurablePropertyOfDescobjIsStringObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-99.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnExistingDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertySeveralAttributesValuesOfPAndPropertiesAreDifferent8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-100.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyBothPropertiesGetAndPGetAreTwoDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-101.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyPGetIsPresentAndPropertiesGetIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-102.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyPGetIsUndefinedAndPropertiesGetIsNormalValue8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-103.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyBothPropertiesSetAndPSetAreTwoDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-104.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyPSetIsPresentAndPropertiesSetIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-105.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyPSetIsUndefinedAndPropertiesSetIsNormalValue8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-106.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyPEnumerableAndPropertiesEnumerableAreDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-107.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyPConfigurableIsTrueAndPropertiesConfigurableIsFalse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-108.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertySeveralAttributesValuesOfPAndPropertiesAreDifferent8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-109.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsInheritedAccessorPropertyWithoutAGetFunction8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesAllOwnPropertiesDataPropertyAndAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-110.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesEachPropertiesAreInListOrder()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-111.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayTestTheLengthPropertyOfOIsOwnDataProperty15451Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-112.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayTestTheLengthPropertyOfOIsOwnDataPropertyThatOverridesAnInheritedDataProperty15451Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-113.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestEveryFieldInDescIsAbsent15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-114.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestEveryFieldInDescIsSameWithCorrespondingAttributeValueOfTheLengthPropertyInO15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-115.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeOfTheLengthPropertyFromFalseToTrue15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-116.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeOfTheLengthPropertyFromFalseToTrue15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-117.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestTypeerrorIsThrownWhenDescIsAccessorDescriptor15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-118.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeOfTheLengthPropertyFromFalseToTrue15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-119.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAFunctionObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsAbsentTestUpdatingTheWritableAttributeOfTheLengthPropertyFromTrueToFalse15451Step3AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-120.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenSettingTheValueFieldOfDescToUndefined15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-121.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTestSettingTheValueFieldOfDescToNullActuallIsSetTo015451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-122.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsABooleanWithValueFalse15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-123.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsABooleanWithValueTrue15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-124.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIs015451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-125.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIs015451Step3C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-126.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIs015451Step3C3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-127.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsPositiveNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-128.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsNegativeNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-129.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsInfinity15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-130.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsInfinity15451Step3C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-131.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsNan15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-132.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingAPositiveNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-133.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingANegativeNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-134.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingADecimalNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-135.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingInfinity15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-136.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingInfinity15451Step3C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-137.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingAnExponentialNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-138.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingAnHexNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-139.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAStringObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringContainingAnLeadingZeroNumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-140.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAStringWhichDoesnTConvertToANumber15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-141.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnTostringMethod15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-142.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnValueofMethod15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-143.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnValueofMethodThatReturnsAnObjectAndTostringMethodThatReturnsAString15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-144.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsAnObjectWhichHasAnOwnTostringAndValueofMethod15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-145.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTypeerrorIsThrownWhenTheValueFieldOfDescIsAnObjectThatBothTostringAndValueofWouldnTReturnPrimitiveValue15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-146.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestUsingInheritedValueofMethodWhenTheValueFieldOfDescIsAnObjecWithAnOwnTostringAndInheritedValueofMethods15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-147.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsPositiveNonIntegerValues15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-148.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsNegativeNonIntegerValues15451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-149.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsABooleanObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsBoundaryValue232215451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-150.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestTheValueFieldOfDescIsBoundaryValue232115451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-151.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsBoundaryValue23215451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-152.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsTheLengthPropertyOfOTestRangeerrorIsThrownWhenTheValueFieldOfDescIsBoundaryValue232115451Step3C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-153.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTestTheValueFieldOfDescWhichIsGreaterThanValueOfTheLengthPropertyIsDefinedIntoOWithoutDeletingAnyPropertyWithLargeIndexNamed15451Step3F()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-155.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTestTheValueFieldOfDescWhichEqualsToValueOfTheLengthPropertyIsDefinedIntoOWithoutDeletingAnyPropertyWithLargeIndexNamed15451Step3F()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-156.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTestTheValueFieldOfDescWhichIsLessThanValueOfTheLengthPropertyIsDefinedIntoOWithDeletingPropertiesWithLargeIndexNamed15451Step3F()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-157.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsGreaterThanValueOfTheLengthPropertyTestTypeerrorIsThrownWhenTheLengthPropertyIsNotWritable15451Step3FI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-158.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescEqualsToValueOfTheLengthPropertyTestTypeerrorWouldnTBeThrownWhenTheLengthPropertyIsNotWritable15451Step3FI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-159.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsANumberObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTypeerrorIsThrownWhenTheWritableAttributeOfTheLengthPropertyIsFalse15451Step3G()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-160.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToTrueAtLastAfterDeletingPropertiesWithLargeIndexNamedIfTheWritableFieldOfDescIsAbsent15451Step3H()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-161.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToTrueAtLastAfterDeletingPropertiesWithLargeIndexNamedIfTheWritableFieldOfDescIsTrue15451Step3H()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-162.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToFalseAtLastAfterDeletingPropertiesWithLargeIndexNamedIfTheWritableFieldOfDescIsFalse15451Step3IIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-163.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyInOIsSetAsTrueBeforeDeletingPropertiesWithLargeIndexNamed15451Step3IIii()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-164.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheLengthPropertyIsDecreasedBy115451Step3LI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-165.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnDataPropertyWithLargeIndexNamedInOCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-166.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfInheritedDataPropertyWithLargeIndexNamedInOCanTStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-167.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnDataPropertyWithLargeIndexNamedInOThatOverridesInheritedDataPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-168.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnDataPropertyWithLargeIndexNamedInOThatOverridesInheritedAccessorPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-169.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsTheMathObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnAccessorPropertyWithLargeIndexNamedInOCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-170.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfInheritedAccessorPropertyWithLargeIndexNamedInOCanTStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-171.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnAccessorPropertyWithLargeIndexNamedInOThatOverridesInheritedDataPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-172.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableAttributeOfOwnAccessorPropertyWithLargeIndexNamedInOThatOverridesInheritedAccessorPropertyCanStopDeletingIndexNamedProperties15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-173.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheConfigurableLargeIndexNamedPropertyOfOCanBeDeleted15451Step3LIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-174.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestValueOfTheLengthPropertyIsSetToTheLastNonConfigurableIndexNamedPropertyOfOPlus115451Step3LIii1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-175.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToFalseAtLastWhenTheWritableFieldOfDescIsFalseAndOContainsNonConfigurableLargeIndexNamedProperty15451Step3LIii2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-176.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsTheLengthPropertyOfOTheValueFieldOfDescIsLessThanValueOfTheLengthPropertyTestTheWritableAttributeOfTheLengthPropertyIsSetToFalseAtLastWhenTheWritableFieldOfDescIsFalseAndODoesnTContainNonConfigurableLargeIndexNamedProperty15451Step3M()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-177.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsAvailableStringValuesThatConvertToNumbers15451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-178.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsBoundaryValue232215451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-179.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsADateObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsBoundaryValue232115451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-180.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsBoundaryValue23215451Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-181.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsBoundaryValue232115451Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-182.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsNotThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyWritableAttributeOfTheLengthPropertyInOIsFalseValueOfPIsLessThanValueOfTheLengthPropertyInO15451Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-183.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyWritableAttributeOfTheLengthPropertyInOIsFalseValueOfPIsEqualToValueOfTheLengthPropertyInO15451Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-184.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyWritableAttributeOfTheLengthPropertyInOIsFalseValueOfPIsBiggerThanValueOfTheLengthPropertyInO15451Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-185.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsOwnDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-186.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsInheritedDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-187.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsOwnDataPropertyThatOverridesAnInheritedDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-188.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-189.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsARegexpObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsOwnAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-190.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyPIsInheritedAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-191.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-192.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-193.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestTypeerrorIsThrownWhenOIsNotExtensible15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-194.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestPIsDefinedAsDataPropertyWhenDescIsGenericDescriptor15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-195.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestValueOfPPropertyInAttributesIsSetAsUndefinedValueIfValueIsAbsentInDataDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-196.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestWritableOfPPropertyInAttributesIsSetAsFalseValueIfWritableIsAbsentInDataDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-197.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestEnumerableOfPPropertyInAttributesIsSetAsFalseValueIfEnumerableIsAbsentInDataDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-198.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestConfigurableOfPPropertyInAttributesIsSetAsFalseValueIfConfigurableIsAbsentInDataDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-199.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsInheritedDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAJsonObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyDescIsDataDescriptorTestUpdatingAllAttributeValuesOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-200.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestGetOfPPropertyInAttributesIsSetAsUndefinedValueIfGetIsAbsentInAccessorDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-201.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestSetOfPPropertyInAttributesIsSetAsUndefinedValueIfSetIsAbsentInAccessorDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-202.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestEnumerableOfPPropertyInAttributesIsSetAsFalseValueIfEnumerableIsAbsentInAccessorDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-203.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPPropertyDoesnTExistInOTestConfigurableOfPPropertyInAttributesIsSetAsFalseValueIfConfigurableIsAbsentInAccessorDescriptorDesc15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-204.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyDescIsAccessorDescriptorTestUpdatingAllAttributeValuesOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-205.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPMakesNoChangeIfEveryFieldInDescIsAbsentNameIsDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-206.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPMakesNoChangeIfEveryFieldInDescIsAbsentNameIsAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-207.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPMakesNoChangeIfTheValueOfEveryFieldInDescIsTheSameValueAsTheCorrespondingFieldInPDescIsDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-208.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyPMakesNoChangeIfTheValueOfEveryFieldInDescIsTheSameValueAsTheCorrespondingFieldInPDescIsAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-209.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnErrorObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyBothTheValueFieldOfDescAndTheValueAttributeValueOfNameAreUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-210.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyBothTheValueFieldOfDescAndTheValueAttributeValueOfNameAreNull15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-211.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyBothTheValueFieldOfDescAndTheValueAttributeValueOfNameAreNan15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-212.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescIs0AndTheValueAttributeValueOfNameIs015451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-213.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescIs0AndTheValueAttributeValueOfNameIs015451Step4C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-214.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoNumbersWithSameVaule15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-215.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoStringsWhichHaveSameLengthAndSameCharactersInCorrespondingPositions15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-216.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoBooleansWithSameValue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-217.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayNameIsAnArrayIndexPropertyTheValueFieldOfDescAndTheValueAttributeValueOfNameAreTwoObjectsWhichReferToTheSameObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-218.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyThatAlreadyExistsOnOWithWritableTrueAndTheWritableFieldOfDescIsTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-219.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsTheArgumentsObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyThatAlreadyExistsOnOWithWritableTrueAndTheWritableFieldOfDescIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-220.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyTheGetFieldOfDescAndTheGetAttributeValueOfPAreTwoObjectsWhichReferToTheSameObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-221.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyTheSetFieldOfDescAndTheSetAttributeValueOfPAreTwoObjectsWhichReferToTheSameObject15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-222.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyThatAlreadyExistsOnOWithEnumerableTrueTheEnumerableFieldOfDescIsTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-223.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyThatAlreadyExistsOnOWithEnumerableTrueTheEnumerableFieldOfDescIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-224.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyThatAlreadyExistsOnOWithConfigurableTrueTheConfigurableFieldOfDescIsTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-225.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyThatAlreadyExistsOnOWithConfigurableTrueTheConfigurableFieldOfDescIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-226.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyTypeerrorIsThrownIfTheConfigurableAttributeValueOfPIsFalseAndTheConfigurableFieldOfDescIsTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-227.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyTypeerrorIsThrownIfTheConfigurableAttributeValueOfPIsFalseAndEnumerableOfDescIsPresentAndItsValueIsDifferentFromTheEnumerableAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-228.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyTypeerrorIsThrownIfPIsAccessorPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfPIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-229.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyTypeerrorIsThrownIfPIsDataPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfPIsFalse15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-230.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyPIsDataPropertyAndDescIsAccessorDescriptorAndTheConfigurableAttributeValueOfPIsTrueTestPIsConvertedFromDataPropertyToAccessorProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-231.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyPIsAccessorPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfPIsTrueTestPIsConvertedFromAccessorPropertyToDataProperty15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-232.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyPIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfPIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfPIsFalseAndTheWritableFieldOfDescIsTrue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-233.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexPropertyPIsDataPropertyAndDescIsDataDescriptorAndTheConfigurableAttributeValueOfPIsFalseTestTypeerrorIsThrownIfTheWritableAttributeValueOfPIsFalseAndTheTypeOfTheValueFieldOfDescIsDifferentFromTheTypeOfTheValueAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-234.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyWithConfigurableWritableFalseDescIsDataDescriptorValueFieldOfDescIs0AndTheValueAttributeValueOfPIs015451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-235.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyWithConfigurableWritableFalseDescIsDataDescriptorValueFieldOfDescIs0AndTheValueAttributeValueOfPIs015451Step4C2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-236.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyWithConfigurableWritableFalseDescIsDataDescriptorValueFieldOfDescAndTheValueAttributeValueOfPAreTwoNumbersWithDifferentVaule15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-237.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyWithConfigurableWritableFalseDescIsDataDescriptorValueFieldOfDescAndTheValueAttributeValueOfPAreTwoStringsWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-238.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyWithConfigurableWritableFalseDescIsDataDescriptorValueFieldOfDescAndTheValueAttributeValueOfPAreTwoBooleansWithDifferentValues15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-239.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsTheGlobalObjectWhichImplementsItsOwnGetownpropertyMethodToGetP8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyWithConfigurableWritableFalseDescIsDataDescriptorValueFieldOfDescAndTheValueAttributeValueOfPAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-240.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyWithConfigurableFalseDescIsAccessorDescriptorTheSetFieldOfDescIsPresentAndTheSetFieldOfDescAndTheSetAttributeValueOfPAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-241.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyWithConfigurableFalseDescIsAccessorDescriptorTheSetFieldOfDescIsPresentAndTheSetFieldOfDescIsAnObjectAndTheSetAttributeValueOfPIsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-242.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsNotThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyWithConfigurableFalseDescIsAccessorDescriptorTheSetFieldOfDescIsPresentAndTheSetFieldOfDescAndTheSetAttributeValueOfPAreUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-243.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyWithConfigurableFalseDescIsAccessorDescriptorTheGetFieldOfDescIsPresentAndTheGetFieldOfDescAndTheGetAttributeValueOfPAreTwoObjectsWhichReferToTheDifferentObjects15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-244.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyWithConfigurableFalseDescIsAccessorDescriptorTheGetFieldOfDescIsPresentAndTheGetFieldOfDescIsAnObjectAndTheGetAttributeValueOfPIsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-245.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeerrorIsNotThrownIfOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyWithConfigurableFalseDescIsAccessorDescriptorTestTypeerrorIsNotThrownIfTheGetFieldOfDescIsPresentAndTheGetFieldOfDescAndTheGetAttributeValueOfPAreUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-246.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheValueAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-247.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyAndDescIsDataDescriptorTestSettingTheValueAttributeValueOfPAsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-248.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyAndDescIsDataDescriptorTestSettingTheValueAttributeValueOfPFromUndefinedToNormalValue15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-249.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestTypeerrorIsThrownWhenOIsNotExtensible8129Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheWritableAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-250.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheEnumerableAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-251.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyAndDescIsDataDescriptorTestUpdatingTheConfigurableAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-252.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsDataPropertyAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-253.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheGetAttributeValueOfPWithDifferentGetterFunction15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-254.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestSettingTheGetAttributeValueOfPAsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-255.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheGetAttributeValueOfPFromUndefinedToFunction15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-256.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheSetAttributeValueOfPWithDifferentGetterFunction15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-257.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestSettingTheSetAttributeValueOfPAsUndefined15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-258.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheSetAttributeValueOfPFromUndefinedToFunction15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-259.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestPIsDefinedAsDataPropertyWhenDescIsGenericDescriptor8129Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheEnumerableAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-260.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingTheConfigurableAttributeValueOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-261.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyThatAlreadyExistsOnOIsAccessorPropertyAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfP15451Step4C()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-262.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyTestTheLengthPropertyOfOIsNotChangedIfTouint32PIsLessThanValueOfTheLengthPropertyInO15451Step4E()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-263.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyTestTheLengthPropertyOfOIsSetAsTouint32P1IfTouint32PEqualsToValueOfTheLengthPropertyInO15451Step4EIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-264.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsAnArrayIndexNamedPropertyTestTheLengthPropertyOfOIsSetAsTouint32P1IfTouint32PIsGreaterThanValueOfTheLengthPropertyInO15451Step4EIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-265.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericPropertyThatWonTExistOnOAndDescIsDataDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-266.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericPropertyAndDescIsAccessorDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-267.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnAccessorPropertyOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfP15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-268.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnDataPropertyOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfP15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-269.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestValueOfPIsSetAsUndefinedValueIfAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfPWhichIsDefinedAsUnwritableAndNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-270.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfPWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-271.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-272.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-273.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfPWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-274.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfPWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-275.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-276.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArrayPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsDefinedAsNonConfigurable15451Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-277.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnPropertyWhichIsEverDefinedInBothParametermapOfOAndOAndIsDeletedAfterwardsAndDescIsDataDescriptorTestPIsRedefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-278.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnPropertyWhichIsEverDefinedInBothParametermapOfOAndOAndIsDeletedAfterwardsAndDescIsAccessorDescriptorTestPIsRedefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-279.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestWritableOfPIsSetAsFalseValueIfAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-280.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-281.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfPWhoseWritableAndConfigurableAttributesAreFalse106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-282.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfPWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-283.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-284.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnDataPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-285.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfPWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-286.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfPWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-287.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-288.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsOwnAccessorPropertyOfOWhichIsAlsoDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsDefinedAsNonConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-289.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestEnumerableOfPIsSetAsFalseValueIfAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedPropertyOfOButNotDefinedInParametermapOfOAndDescIsDataDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-290.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedPropertyOfOButNotDefinedInParametermapOfOAndDescIsAccessorDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-291.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-292.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-293.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfPWhichIsNotWritableAndNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-294.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-295.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-296.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedDataPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-297.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-298.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-299.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnDataPropertyThatOverridesAnInheritedDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestConfigurableOfPIsSetAsFalseValueIfAbsentInDataDescriptorDesc8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-300.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsAnArrayIndexNamedAccessorPropertyOfOButNotDefinedInParametermapOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-301.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericPropertyAndDescIsDataDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-302.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericPropertyAndDescIsAccessorDescriptorTestPIsDefinedInOWithAllCorrectAttributeValues106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-303.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnAccessorPropertyOfOAndDescIsAccessorDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-304.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnDataPropertyOfOAndDescIsDataDescriptorTestUpdatingMultipleAttributeValuesOfP106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-305.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheValueAttributeValueOfPWhichIsNotWritableAndNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-306.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheWritableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-307.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-308.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnDataPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-309.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescIsDataDescriptorTestSettingAllAttributeValuesOfP8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheGetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-310.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheSetAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-311.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheEnumerableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-312.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectPIsGenericOwnAccessorPropertyOfOTestTypeerrorIsThrownWhenUpdatingTheConfigurableAttributeValueOfPWhichIsNotConfigurable106DefineownpropertyStep4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-313.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesOIsAnArgumentsObjectNameIsOwnPropertyOfParametermapOfOTestNameIsDeletedIfNameIsConfigurableAndDescIsAccessorDescriptor106DefineownpropertyStep5AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-314.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescIsGenericDescriptorWithoutAnyAttributeTestPIsDefinedInObjWithAllDefaultAttributeValues8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestGetOfPIsSetAsUndefinedValueIfAbsentInAccessorDescriptorDesc8129Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestSetOfPIsSetAsUndefinedValueIfAbsentInAccessorDescriptorDesc8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestEnumerableOfPIsSetAsFalseValueIfAbsentInAccessorDescriptorDesc8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPDoesnTExistInOTestConfigurableOfPIsSetAsFalseValueIfAbsentInAccessorDescriptorDesc8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescIsAccessorDescriptorTestSettingAllAttributeValuesOfP8129Step4BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPExistsInOIsAnAccessorPropertyTestPMakesNoChangeIfDescIsGenericDescriptorWithoutAnyAttribute8129Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-38-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPExistsInOTestPMakesNoChangeIfDescIsGenericDescriptorWithoutAnyAttribute8129Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataDescriptorAndEveryFieldsInDescIsTheSameWithP8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorDescriptorAndEveryFieldsInDescIsTheSameWithP8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesTypeOfDescValueIsDifferentFromTypeOfPValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescValueAndPValueAreUndefined8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescValueAndPValueAreNull8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescValueAndPValueAreNan8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescValueIs0AndPValueIs08129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescValueIs0AndPValueIs08129Step62()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescValueAndPValueAreTwoNumbersWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescValueAndPValueAreTwoNumbersWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescValueAndPValueAreTwoStringsWhichHaveSameLengthAndSameCharactersInCorrespondingPositions8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescValueAndPValueAreTwoStringsWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescValueAndPValueAreBooleanValuesWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescValueAndPValueAreTwoBooleanValuesWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-52.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescValueAndPValueAreOjbectsWhichReferToTheSameObject8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-53.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescValueAndPValueAreTwoOjbectsWhichReferToTheDifferentObjects8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-54.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescWritableAndPWritableAreBooleanValuesWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-55.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescWritableAndPWritableAreTwoBooleanValuesWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-56.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescGetAndPGetAreTwoObjectsWhichReferToTheSameObject8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-57.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescGetAndPGetAreTwoObjectsWhichReferToTheDifferentObjects8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-58.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescSetAndPSetAreTwoObjectsWhichReferToTheSameObject8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-59.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescSetAndPSetAreTwoObjectsWhichReferToTheDifferentObjects8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-60.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescEnumerableAndPEnumerableAreBooleanValuesWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-61.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescEnumerableAndPEnumerableAreTwoBooleanValuesWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-62.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesBothDescConfigurableAndPConfigurableAreBooleanValuesWithTheSameValue8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-63.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesDescConfigurableAndPConfigurableAreTwoBooleanValuesWithDifferentValues8129Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-64.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalseAndDescConfigurableIsTrue8129Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-65.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePEnumerableAndDescEnumerableHasDifferentValues8129Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-66-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePEnumerableAndDescEnumerableHasDifferentValues8129Step7B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-66.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPIsAccessorPropertyAndPConfigurableIsFalseDescIsDataProperty8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-67.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPIsDataPropertyAndPConfigurableIsFalseDescIsAccessorProperty8129Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-68.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertyAndPConfigurableIsTrueDescIsAccessorProperty8129Step9BI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-69.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsAccessorPropertyAndPConfigurableIsTrueDescInPropertiesIsDataProperty8129Step9CI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-70.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPIsDataPropertyAndPConfigurableIsFalsePWritableIsFalseDescIsDataPropertyAndDescWritableIsTrue8129Step10AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-71.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPIsDataPropertyAndPConfigurableIsFalsePWritableIsFalseDescIsDataPropertyAndDescValueIsNotEqualToPValue8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-72.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorIfPConfigurableIsFalsePWritalbeIsFalsePValueIsUndefinedAndPropertiesValueIsUndefined8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-73.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorIfPConfigurableIsFalsePWritalbeIsFalsePValueIsNullAndPropertiesValueIsNull8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-74.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorIfPConfigurableIsFalsePWritalbeIsFalsePValueIsNanAndPropertiesValueIsNan8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-75.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueIs0AndPValueIs08129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-76.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueIs0AndPValueIs08129Step10AIi12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-77.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoNumbersWithTheSameValue8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-78.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoNumbersWithDifferentValues8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-79.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoStringsWithTheSameValue8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-80.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoStringsWithDifferentValues8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-81.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoBooleansWithTheSameValue8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-82.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoBooleansWithDifferentValues8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-83.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoObjectsReferToTheSameObjectWhichHasBeenUpdatedBeforeUseItToUpdateTheObject8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-84-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoObjectsReferToTheSameObject8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-84.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePWritalbeIsFalsePropertiesValueAndPValueAreTwoObjectsWithDifferentValues8129Step10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-85.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalseBothPropertiesSetAndPSetAreTwoObjectsWhichReferToTheSameObjectAndTheObjectHasBeenUpdatedAfterDefined8129Step11AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-86-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalseBothPropertiesSetAndPSetAreTwoObjectsWhichReferToTheSameObject8129Step11AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-86.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalseBothPropertiesSetAndPSetAreTwoObjectsWhichReferToDifferentObjects8129Step11AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-87.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePSetIsUndefinedPropertiesSetRefersToAnObjcet8129Step11AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-88.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalsePSetAndPropertiesSetAreUndefined8129Step11AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-89.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsOwnAccessorPropertyWithoutAGetFunction8129Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalseBothPropertiesGetAndPGetAreTwoObjectsWhichReferToTheSameObject8129Step11AIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-90.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalseBothPropertiesGetAndPGetAreTwoObjectsWhichReferToDifferentObjects8129Step11AIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-91.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesThrowsTypeerrorWhenPConfigurableIsFalsePGetIsUndefinedPropertiesGetRefersToAnObjcet8129Step11AIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-92.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillUpdateValueAttributeOfNamedDataPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseButNotWhenBothAreFalse8129StepNote10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-93-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillUpdateValueAttributeOfIndexedDataPropertyPSuccessfullyWhenConfigurableAttributeIsTrueAndWritableAttributeIsFalseButNotWhenBothAreFalse8129StepNote10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-93-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillFailToUpdateValueAttributeOfNamedDataPropertyPWhenConfigurableAttributeOfFirstUpdatingPropertyIsFalse8129StepNote10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-93-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillFailToUpdateValueAttributeOfIndexedDataPropertyPWhenConfigurableAttributeOfFirstUpdatingPropertyAreFalse8129StepNote10AIi1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-93-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesWillNotThrowTypeerrorWhenPConfigurableIsFalsePGetAndPropertiesGetAreUndefined8129Step11AIi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-93.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertyPropertiesValueAndPValueAreTwoDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-94.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertyPValueIsPresentAndPropertiesValueIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-95.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertyPropertiesValueIsPresentAndPValueIsUndefined8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-96.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertyPWritableAndPropertiesWritableAreDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-97.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertyPEnumerableAndPropertiesEnumerableAreDifferentValues8129Step12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-98.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.7")]
        public void ObjectDefinepropertiesPIsDataPropertyPConfigurableIsTrueAndPropertiesConfigurableIsFalse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-99.js", false);
        }


    }
}
