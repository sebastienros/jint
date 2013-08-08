using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesThrowsTypeerrorIfOIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesThrowsTypeerrorIfOIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesThrowsTypeerrorIfOIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesThrowsTypeerrorIfOIsAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesReturnedArrayIsAnArrayAccordingToArrayIsarray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesReturnedArrayIsAnInstanceOfArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesLengthOfReturnedArrayIsInitializedTo0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesReturnedArrayIsTheStandardBuiltInConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesElementsOfTheReturnedArrayStartFromIndex0()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesReturnsArrayOfPropertyNamesGlobal()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesReturnsArrayOfPropertyNamesObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesInheritedDataPropertiesAreNotPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesInheritedAccessorPropertiesAreNotPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnDataPropertiesArePushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnAccessorPropertiesArePushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesInheritedDataPropertyOfStringObjectOIsNotPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesInheritedAccessorPropertyOfStringObjectOIsNotPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnDataPropertyOfStringObjectOIsPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnAccessorPropertyOfStringObjectOIsPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnIndexPropertiesOfStringObjectArePushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesInheritedDataPropertyOfArrayObjectOIsNotPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesInheritedAccessorPropertyOfArrayObjectOIsNotPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnDataPropertyOfArrayObjectOIsPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnAccessorPropertyOfArrayObjectOIsPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnIndexPropertiesOfArrayObjcectArePushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesNonEnumerableOwnPropertyOfOIsPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesDescriptorOfResultantArrayIsAllTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesAllOwnPropertiesArePushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesOwnPropertyNamedEmptyIsPushedIntoTheReturnedArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesElementsOfTheReturnedArrayAreWritable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesElementsOfTheReturnedArrayAreEnumerable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertynamesElementsOfTheReturnedArrayAreConfigurable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.4")]
        public void ObjectGetownpropertiesAndObjectPrototypeHasownpropertyShouldAgreeOnWhatTheOwnPropertiesAre()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.4/S15.2.3.4_A1_T1.js", false);
        }


    }
}
