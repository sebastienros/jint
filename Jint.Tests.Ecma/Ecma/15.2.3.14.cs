using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysThrowsTypeerrorIfTypeOfFirstParamIsNotObjectBoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysThrowsTypeerrorIfTypeOfFirstParamIsNotObjectString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysThrowsTypeerrorIfTypeOfFirstParamIsNotObjectNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysThrowsTypeerrorIfTypeOfFirstParamIsNotObjectUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayCheckClass()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayArrayOverridden()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayThatIsExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayThatIsNotSealed()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayThatIsNotFrozen()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysNIs0WhenODoesnTContainOwnEnumerableDataOrAccessorProperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysNIsTheCorrectValueWhenEnumerablePropertiesExistInO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayContainingOwnEnumerableProperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayContainingOwnEnumerablePropertiesFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayContainingOwnEnumerablePropertiesArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOfAnArgumentsObjectReturnsTheIndicesOfTheGivenArguments()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysMustReturnAFreshArrayOnEachInvocation()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysReturnsTheStandardBuiltInArrayInstanceofArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysLengthOfTheReturnedArrayEqualsTheNumberOfOwnEnumerablePropertiesOfO()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysElementsOfTheReturnedArrayStartFromIndex0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableDataPropertyOfOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysInhertedEnumerableAccessorPropertyThatIsOverRiddenByNonEnumerableOwnAccessorPropertyIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableIndexedDataPropertyOfDenseArrayOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableIndexedAccessorPropertyOfDenseArrayOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableIndexedDataPropertyOfSparseArrayOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableIndexedAccessorPropertyOfSparseArrayOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableIndexedDataPropertyOfStringObjectOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableIndexedAccessorPropertyOfStringObjectOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysOwnEnumerableAccessorPropertyOfOIsDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysNonEnumerableOwnDataPropertyOfOIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysNonEnumerableOwnAccessorPropertyOfOIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysInheritedEnumerableDataPropertyOfOIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysInheritedEnumerableAccessorPropertyOfOIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysInhertedEnumerableDataPropertyThatIsOverRiddenByNonEnumerableOwnDataPropertyIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysInhertedEnumerableDataPropertyThatIsOverRiddenByNonEnumerableOwnAccessorPropertyIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysInhertedEnumerableAccessorPropertyThatIsOverRiddenByNonEnumerableOwnDataPropertyIsNotDefinedInReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysValueAttributeOfElementInReturnedArrayIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysWritableAttributeOfElementOfReturnedArrayIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysEnumerableAttributeOfElementOfReturnedArrayIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysVerifyThatConfigurableAttributeOfElementOfReturnedArrayIsCorrect()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysVerifyThatIndexOfReturnedArrayIsAscendBy1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysTheOrderOfElementsInReturnedArrayIsTheSameWithTheOrderOfPropertiesInODenseArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysTheOrderOfElementsInReturnedArrayIsTheSameWithTheOrderOfPropertiesInOSparseArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysTheOrderOfElementsInReturnedArrayIsTheSameWithTheOrderOfPropertiesInOStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-6-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysTheOrderOfElementsInReturnedArrayIsTheSameWithTheOrderOfPropertiesInOArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-6-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysTheOrderOfElementsInReturnedArrayIsTheSameWithTheOrderOfPropertiesInOAnyOtherBuiltInObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-6-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.14")]
        public void ObjectKeysTheOrderOfElementsInReturnedArrayIsTheSameWithTheOrderOfPropertiesInOGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-6-6.js", false);
        }


    }
}
