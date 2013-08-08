using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateMustExistAsAFunctionTaking2Parameters()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateThrowsTypeerrorIfOIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateTypeerrorIsNotThrownIfOIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateThrowsTypeerrorIfOIsABooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateThrowsTypeerrorIfOIsANumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void CreateSetsThePrototypeOfTheCreatedObjectToFirstParameterThisCanBeCheckedUsingIsprototypeofOrGetprototypeof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateReturnedObjectIsAnInstanceOfObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void CreateSetsThePrototypeOfTheCreatedObjectToFirstParameterThisCanBeCheckedUsingIsprototypeofOrGetprototypeof2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void CreateSetsThePrototypeOfTheCreatedObjectToFirstParameterThisCanBeCheckedUsingIsprototypeofOrGetprototypeof3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsTheMathObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsNotPresent8105Step4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-100.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-101.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-102.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-103.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-104.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-105.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-106.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-107.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-108.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunction8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-109.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsADateObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunctionWhichOverridesAnInheritedAccessorProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-110.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-111.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-112.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-113.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAStringObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-114.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-115.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsANumberObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-116.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-117.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsADateObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-118.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsADateObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-119.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsARegexpObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-120.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-121.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-122.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheConfigurableProperty8105Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-124.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsUndefined8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-125.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsNull8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-126.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsTrue8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-127.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsFalse8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-128.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIs08105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-129.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsTheJsonObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIs08105Step4B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-130.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIs08105Step4B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-131.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsNan8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-132.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAPositiveNumber8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-133.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsANegativeNumber8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-134.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAnEmptyString8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-135.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsANonEmptyString8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-136.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAFunctionObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-137.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAnArrayObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-138.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAStringObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-139.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsAnErrorObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsABooleanObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-140.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsANumberObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-141.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsTheMathObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-142.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsADateObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-143.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsARegexpObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-144.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsTheJsonObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-145.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAnErrorObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-146.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAnArgumentsObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-147.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsTheGlobalObject8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-149.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsTheAgumentsObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsAStringValueIsFalseWhichIsTreatedAsTheValueTrue8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-150.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsNewBooleanFalseWhichIsTreatedAsTheValueTrue8105Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-151.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsPresent8105Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-152.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsNotPresent8105Step5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-153.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-154.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-155.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-156.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-157.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-158.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-159.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnEnumerableDataPropertyInPropertiesIsDefinedInObj15237Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-160.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-161.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunction8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-162.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunctionWhichOverridesAnInheritedAccessorProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-163.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValuePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-164.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-165.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-166.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAStringObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-167.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-168.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsANumberObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-169.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnDataPropertyInPropertiesWhichIsNotEnumerableIsNotDefinedInObj15237Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-170.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsADateObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-171.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-172.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-173.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-174.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-175.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheValueProperty8105Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-177.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsTrue8105Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-178.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsNotPresent8105Step6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-179.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateAnEnumerableInheritedDataPropertyInPropertiesIsNotDefinedInObj15237Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-180.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-181.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-182.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-183.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-184.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-185.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-186.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-187.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunction8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-188.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunctionWhichOverridesAnInheritedAccessorProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-189.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnEnumerableAccessorPropertyInPropertiesIsDefinedInObj15237Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-190.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-191.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-192.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAStringObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-193.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-194.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsANumberObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-195.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-196.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsADateObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-197.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-198.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-199.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnAccessorPropertyInPropertiesWhichIsNotEnumerableIsNotDefinedInObj15237Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-200.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-201.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheWritableProperty8105Step6A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-203.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsUndefined8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-204.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsNull8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-205.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsTrue8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-206.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsFalse8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-207.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIs08105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-208.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIs08105Step6B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-209.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateAnEnumerableInheritedAccessorPropertyInPropertiesIsNotDefinedInObj15237Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIs08105Step6B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-210.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsNan8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-211.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAPositiveNumberPrimitive8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-212.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsANegativeNumberPrimitive8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-213.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAnEmptyString8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-214.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsANonEmptyString8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-215.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAFunctionObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-216.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAnArrayObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-217.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAStringObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-218.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsABooleanObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-219.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnEnumerableDataPropertyThatOverridesAnEnumerableInheritedDataPropertyInPropertiesIsDefinedInObj15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsANumberObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-220.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsTheMathObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-221.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsADateObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-222.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsARegexpObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-223.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsTheJsonObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-224.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAnErrorObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-225.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAnArgumentsObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-226.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsTheGlobalObject8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-228.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsAStringValueIsFalseWhichIsTreatedAsTheValueTrue8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-229.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnEnumerableDataPropertyThatOverridesAnEnumerableInheritedAccessorPropertyInPropertiesIsDefinedInObj15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritablePropertyOfOnePropertyInPropertiesIsNewBooleanFalseWhichIsTreatedAsTheValueTrue8105Step6B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-230.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsPresent8105Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-231.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsNotPresent8105Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-232.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-233.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-234.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-235.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-236.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-237.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-238.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-239.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnEnumerableAccessorPropertyThatOverridesAnEnumerableInheritedDataPropertyInPropertiesIsDefinedInObj15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-240.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunction8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-241.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunctionWhichOverridesAnInheritedAccessorProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-242.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-243.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-244.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-245.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAStringObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-246.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-247.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsANumberObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-248.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsADateObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-249.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnEnumerableAccessorPropertyThatOverridesAnEnumerableInheritedAccessorPropertyInPropertiesIsDefinedInObj15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-250.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-251.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-252.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-253.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-254.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheGetProperty8105Step7A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-256.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsUndefined8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-257.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsThePrimitiveValueNull8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-258.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsABooleanPrimitive8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-259.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateTypeerrorIsThrownWhenOwnEnumerableAccessorPropertyOfPropertiesWithoutAGetFunction15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsANumberPrimitive8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-260.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsAPrimitiveString8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-261.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsAnArrayObject8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-262.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetPropertyOfOnePropertyInPropertiesIsAFunction8105Step7B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-263.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsPresent8105Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-266.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsNotPresent8105Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-267.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-268.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-269.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOwnEnumerableAccessorPropertyInPropertiesWithoutAGetFunctionThatOverridesAnEnumerableInheritedAccessorPropertyInPropertiesIsDefinedInObj15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-270.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-271.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-272.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-273.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-274.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-275.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunction8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-276.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunctionWhichOverridesAnInheritedAccessorProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-277.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-278.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-279.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-280.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAStringObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-281.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-282.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsANumberObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-283.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-284.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsADateObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-285.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-286.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-287.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-288.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-289.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsAnArrayObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheSetProperty8105Step8A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-291.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsUndefined8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-292.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAPrimitiveValueNull8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-293.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAPrimitiveBooleanValueTrue8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-294.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAPrimitiveNumberValue8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-295.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAPrimitiveStringValue8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-296.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAnDateObject8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-297.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAFunction8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-298.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateThrowsTypeerrorIfPropertiesIsNull15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsAStringObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetPropertyOfOnePropertyInPropertiesIsAHostObjectThatIsnTCallable8105Step8B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-300.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateTypeerrorIsThrownIfBothSetPropertyAndValuePropertyOfOnePropertyInPropertiesArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-301.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateTypeerrorIsThrownIfBothSetPropertyAndWritablePropertyOfOnePropertyInPropertiesArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-302.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateTypeerrorIsThrownIfBothGetPropertyAndValuePropertyOfOnePropertyInPropertiesArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-303.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateTypeerrorIsThrownIfBothGetPropertyAndWritablePropertyOfOnePropertyInPropertiesArePresent8105Step9A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-304.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateDefinesADataPropertyWhenOnePropertyInPropertiesIsGenericDescriptor8129Step4A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-305.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValueIsSetAsUndefinedIfItIsAbsentInDataDescriptorOfOnePropertyInProperties8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-306.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateWritableIsSetAsFalseIfItIsAbsentInDataDescriptorOfOnePropertyInProperties8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-307.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerableIsSetAsFalseIfItIsAbsentInDataDescriptorOfOnePropertyInProperties8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-308.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurableIsSetAsFalseIfItIsAbsentInDataDescriptorOfOnePropertyInProperties8129Step4AI()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-309.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsABooleanObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateGetIsSetAsUndefinedIfItIsAbsentInAccessorDescriptorOfOnePropertyInProperties8129Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-310.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSetIsSetAsUndefinedIfItIsAbsentInAccessorDescriptorOfOnePropertyInProperties8129Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-311.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerableIsSetAsFalseIfItIsAbsentInAccessorDescriptorOfOnePropertyInProperties8129Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-312.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurableIsSetAsFalseIfItIsAbsentInAccessorDescriptorOfOnePropertyInProperties8129Step4B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-313.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateSomeEnumerableOwnPropertyInPropertiesIsEmptyObject15237Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-314.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateAllPropertiesInPropertiesAreEnumerableDataPropertyAndAccessorProperty15237Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-315.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertiesOfPropertiesAreGivenNumericalNames15237Step7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-316.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsANumberObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsTheMathObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsADateObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsARegexpObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsTheJsonObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsAnErrorObjectThatUsesObjectSGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreatePropertiesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessOwnEnumerableProperty15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnsureThatSideEffectsOfGetsOccurInTheSameOrderAsTheyWouldForForPInPropsPropsP15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsAnObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnsureThatIfAnExceptionIsThrownItOccursInTheCorrectOrderRelativeToPriorAndSubsequentSideEffects15237Step5A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValueOfOnePropertyInPropertiesIsUndefined8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValueOfOnePropertyInPropertiesIsNull8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValueOfOnePropertyInPropertiesIsFalse8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValueOfOnePropertyInPropertiesIsANumberPrimitive8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValueOfOnePropertyInPropertiesIsAString8105Step1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsTrue8105Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsNotPresent8105Step3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsAFunctionObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-52.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-53.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-54.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-55.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunction8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-56.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsOwnAccessorPropertyWithoutAGetFunctionWhichOverridesAnInheritedAccessorProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-57.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAnInheritedAccessorPropertyWithoutAGetFunction8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-58.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAFunctionObjectWhichImplementsItsOwnGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-59.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsAnArrayObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArrayObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-60.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAStringObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-61.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsABooleanObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-62.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsANumberObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-63.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheMathObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-64.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsADateObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-65.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsARegexpObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-66.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheJsonObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-67.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnErrorObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-68.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsAnArgumentsObjectWhichImplementsItsOwnGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-69.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsAStringObject15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateOnePropertyInPropertiesIsTheGlobalObjectThatUsesObjectSGetMethodToAccessTheEnumerableProperty8105Step3A()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-71.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsUndefined8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-72.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateValueOfEnumerablePropertyOfOnePropertyInPropertiesIsNull8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-73.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsTrue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-74.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsFalse8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-75.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIs08105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-76.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIs08105Step3B2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-77.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIs08105Step3B3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-78.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsNan8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-79.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsABooleanObjectWhosePrimitiveValueIsTrue15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAPositiveNumberPrimitive8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-80.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsANegativeNumberPrimitive8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-81.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAnEmptyString8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-82.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsANonEmptyString8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-83.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAFunctionObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-84.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAnArrayObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-85.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAStringObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-86.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsABooleanObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-87.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsANumberObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-88.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsTheMathObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-89.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateArgumentPropertiesIsANumberObjectWhosePrimitiveValueIsAnyInterestingNumber15237Step2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsADateObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-90.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsARegexpObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-91.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsTheJsonObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-92.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAnErrorObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-93.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAnArgumentsObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-94.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsTheGlobalObject8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-96.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsAStringValueIsFalseWhichIsTreatedAsTheValueTrue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-97.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateEnumerablePropertyOfOnePropertyInPropertiesIsNewBooleanFalseWhichIsTreatedAsTheValueTrue8105Step3B()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-98.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.5")]
        public void ObjectCreateConfigurablePropertyOfOnePropertyInPropertiesIsTrue8105Step4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-99.js", false);
        }


    }
}
