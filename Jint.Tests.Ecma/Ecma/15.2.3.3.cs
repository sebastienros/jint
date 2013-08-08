using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorMustExistAsAFunctionTaking2Parameters()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorTypeerrorIsThrownWhenFirstParamIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorTypeerrorIsThrownWhenFirstParamIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorTypeerrorIsThrownWhenFirstParamIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorTypeerrorIsThrownWhenFirstParamIsANumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsUndefinedForUndefinedPropertyName()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIsPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIsNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIsInfinity3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1Following20Zeros()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1Following21Zeros()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1Following22Zeros()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1E20()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsUndefinedForNullPropertyName()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToStringValueIs1E21()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1E22()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs0000001()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs00000001()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs000000001()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1E7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1E6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1E5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAnIntegerThatConvertsToAStringValueIs123()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsADecimalThatConvertsToAStringValueIs123456()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs100000000000000000000123()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs1231234567()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAppliedToAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAppliedToStringAbCd()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAppliedToStringUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAppliedToStringNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAppliedToString123Cd()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAppliedToString1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAnArrayThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAStringObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsNullThatConvertsToStringNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsABooleanObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAnObjectThatHasAnOwnTostringMethodThatReturnsAnObjectAndTovalueMethodThatReturnsAPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsAnObjectWhichHasAnOwnTostringAndValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorTypeerrorExceptionWasThrownWhenPIsAnObjectThatBothTostringAndValueofWouldnTReturnPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorUsesInheritedTostringMethodWhenPIsAnObjectWithAnOwnValueofAndInheritedTostringMethods()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsABooleanWhoseValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsABooleanWhoseValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs02()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorArgumentPIsANumberThatConvertsToAStringValueIs03()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsNotAnExistingProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorAppliedToTheArgumentsObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorAppliedToAStringObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorAppliedToAFunctionObjectWhichImplementsItsOwnPropertyGetMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorPIsOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsAnObjectRepresentingADataDescForValidDataValuedProperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalDecodeuricomponent()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathAtan2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-100.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathCeil()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-101.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathCos()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-102.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathExp()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-103.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathFloor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-104.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathLog()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-105.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathMax()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-106.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathMin()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-107.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathPow()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-108.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathRandom()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-109.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalEncodeuricomponent()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathRound()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-110.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathSin()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-111.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathSqrt()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-112.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathTan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-113.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDateParse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-114.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDateUtc()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-115.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-116.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGettime()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-117.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGettimezoneoffset()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-118.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetfullyear()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-120.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetmonth()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-121.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetdate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-122.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetday()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-123.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGethours()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-124.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetminutes()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-125.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-126.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-127.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutcfullyear()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-128.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutcmonth()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-129.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutcdate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-130.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutcday()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-131.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutchours()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-132.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutcminutes()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-133.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutcseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-134.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeGetutcmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-135.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSettime()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-136.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetfullyear()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-138.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetmonth()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-139.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectGetprototypeof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetdate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-140.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSethours()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-141.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetminutes()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-142.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-143.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-144.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetutcfullyear()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-145.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetutcmonth()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-146.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetutcdate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-147.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetutchours()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-148.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetutcminutes()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-149.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectGetownpropertydescriptor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetutcseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-150.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeSetutcmilliseconds()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-151.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeTolocalestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-152.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-153.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeToutcstring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-154.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeTotimestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-156.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeTodatestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-157.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeTolocaledatestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-158.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeTolocaletimestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-159.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectGetownpropertynames()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeValueof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-160.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeToisostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-161.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsDatePrototypeTojson()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-162.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsRegexpPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-163.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsRegexpPrototypeExec()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-165.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsRegexpPrototypeTest()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-166.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsRegexpPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-167.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsErrorPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-168.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsErrorPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-169.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectCreate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsEvalerrorPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-170.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsRangeerrorPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-171.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsReferenceerrorPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-172.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsSyntaxerrorPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-173.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsTypeerrorPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-174.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsUrierrorPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-175.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsJsonStringify()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-176.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsJsonParse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-177.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsGlobalNan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-178.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsGlobalInfinity()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-179.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectDefineproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsGlobalUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-180.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsObjectPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-182.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsUndefinedForNonExistentPropertyArguments1OnBuiltInObjectFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-183.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsUndefinedForNonExistentPropertyCallerOnBuiltInObjectMath()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-184.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsFunctionPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-185.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsFunctionLength()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-186.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsFunctionInstanceLength()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-187.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsUndefinedForNonExistentPropertiesOnBuiltInsFunctionInstanceName()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-188.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsArrayPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-189.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectDefineproperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsStringPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-190.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsStringLength()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-191.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsStringInstanceLength()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-192.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsBooleanPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-193.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsBooleanLength()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-194.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsNumberPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-195.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsNumberMaxValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-196.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsNumberMinValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-197.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsNumberNan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-198.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsNumberNegativeInfinity()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-199.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsUndefinedForNonExistentProperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectSeal()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsNumberPositiveInfinity()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-200.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsNumberLength()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-201.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathE()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-202.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathLn10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-203.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathLn2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-204.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathLog2E()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-205.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathLog10E()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-206.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathPi()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-207.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathSqrt12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-208.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsMathSqrt2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-209.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectFreeze()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsDatePrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-210.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsRegexpPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-211.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsRegexpPrototypeSource()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-212.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsRegexpPrototypeGlobal()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-213.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsRegexpPrototypeIgnorecase()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-214.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsRegexpPrototypeMultiline()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-215.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsErrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-216.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsEvalerrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-217.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsRangeerrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-218.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsReferenceerrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-219.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPreventextensions()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsSyntaxerrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-220.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsTypeerrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-221.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescAllFalseForPropertiesOnBuiltInsUrierrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-222.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatValuePropertyOfReturnedObjectIsDataPropertyWithCorrectValueAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-223.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatValuePropertyOfReturnedObjectIsDataPropertyWithCorrectWritableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-224.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatValuePropertyOfReturnedObjectIsDataPropertyWithCorrectEnumerableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-225.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatValuePropertyOfReturnedObjectIsDataPropertyWithCorrectConfigurableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-226.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatWritablePropertyOfReturnedObjectIsDataPropertyWithCorrectValueAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-227.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatWritablePropertyOfReturnedObjectIsDataPropertyWithCorrectWritableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-228.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatWritablePropertyOfReturnedObjectIsDataPropertyWithCorrectEnumerableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-229.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectIssealed()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatWritablePropertyOfReturnedObjectIsDataPropertyWithCorrectConfigurableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-230.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatEnumerablePropertyOfReturnedObjectIsDataPropertyWithCorrectValueAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-231.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatEnumerablePropertyOfReturnedObjectIsDataPropertyWithCorrectWritableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-232.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatEnumerablePropertyOfReturnedObjectIsDataPropertyWithCorrectEnumerableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-233.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatEnumerablePropertyOfReturnedObjectIsDataPropertyWithCorrectConfigurableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-234.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatConfigurablePropertyOfReturnedObjectIsDataPropertyWithCorrectValueAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-235.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatConfigurablePropertyOfReturnedObjectIsDataPropertyWithCorrectWritableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-236.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatConfigurablePropertyOfReturnedObjectIsDataPropertyWithCorrectEnumerableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-237.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatConfigurablePropertyOfReturnedObjectIsDataPropertyWithCorrectConfigurableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-238.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatGetPropertyOfReturnedObjectIsDataPropertyWithCorrectValueAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-239.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectIsfrozen()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatGetPropertyOfReturnedObjectIsDataPropertyWithCorrectWritableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-240.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatGetPropertyOfReturnedObjectIsDataPropertyWithCorrectEnumerableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-241.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatGetPropertyOfReturnedObjectIsDataPropertyWithCorrectConfigurableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-242.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatSetPropertyOfReturnedObjectIsDataPropertyWithCorrectValueAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-243.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatSetPropertyOfReturnedObjectIsDataPropertyWithCorrectWritableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-244.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatSetPropertyOfReturnedObjectIsDataPropertyWithCorrectEnumerableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-245.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorEnsureThatSetPropertyOfReturnedObjectIsDataPropertyWithCorrectConfigurableAttribute()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-246.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnedValueIsAnInstanceOfObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-247.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnedObjectContainsThePropertyValueIfTheValueOfPropertyValueIsNotExplicitlySpecifiedWhenDefinedByObjectDefineproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-248.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnedObjectContainsThePropertySetIfTheValueOfPropertySetIsNotExplicitlySpecifiedWhenDefinedByObjectDefineproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-249.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectIsextensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnedObjectContainsThePropertyGetIfTheValueOfPropertyGetIsNotExplicitlySpecifiedWhenDefinedByObjectDefineproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-250.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectKeys()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPrototypeValueof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsAnObjectRepresentingAnAccessorDescForValidAccessorProperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPrototypeIsprototypeof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPrototypeHasownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPrototypePropertyisenumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsObjectPrototypeTolocalestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsFunctionPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsFunctionPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsFunctionPrototypeApply()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsFunctionPrototypeCall()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalEval()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeConcat()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeJoin()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeReverse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeSlice()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeSort()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypePush()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypePop()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeShift()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeUnshift()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalParseint()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeSplice()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeTolocalestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeIndexof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-52.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeLastindexof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-53.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeEvery()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-54.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeSome()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-55.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeForeach()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-56.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeMap()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-57.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeFilter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-58.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeReduce()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-59.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalParsefloat()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsArrayPrototypeReduceright()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-60.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringFromcharcode()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-61.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-62.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeCharat()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-63.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeCharcodeat()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-64.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeConcat()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-65.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeIndexof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-66.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeLastindexof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-67.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeMatch()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-68.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeReplace()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-69.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalIsnan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeSearch()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-70.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeSlice()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-71.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeSplit()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-72.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeSubstring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-73.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeTolowercase()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-75.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-76.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeTouppercase()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-77.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeValueof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-78.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeTolocalelowercase()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-79.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalIsfinite()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeTolocaleuppercase()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-80.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeLocalecompare()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-81.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsStringPrototypeTrim()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-82.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsBooleanPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-84.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsBooleanPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-85.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsBooleanPrototypeValueof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-86.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsNumberPrototypeConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-88.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsNumberPrototypeTostring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-89.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsGlobalDecodeuri()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsNumberPrototypeTolocalestring()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-90.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsNumberPrototypeTofixed()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-91.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsNumberPrototypeToexponential()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-92.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsNumberPrototypeToprecision()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-93.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsNumberPrototypeValueof()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-94.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathAbs()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-96.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathAcos()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-97.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathAsin()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-98.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.3")]
        public void ObjectGetownpropertydescriptorReturnsDataDescForFunctionsOnBuiltInsMathAtan()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-99.js", false);
        }


    }
}
