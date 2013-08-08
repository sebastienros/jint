using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeThrowsTypeerrorIfTypeOfFirstParamIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeThrowsTypeerrorIfTypeOfFirstParamIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeThrowsTypeerrorIfTypeOfFirstParamIsBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeThrowsTypeerrorIfTypeOfFirstParamIsStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeExtensibleOfOIsSetAsFalseEvenIfOHasNoOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeInheritedDataPropertiesAreNotFrozen()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeInheritedAccessorPropertiesAreNotFrozen()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeNonEnumerableOwnPropertiesOfOAreFrozen()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnNamedPropertyOfAnArrayObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnIndexPropertyOfTheArgumentsObjectThatImplementsItsOwnGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnIndexPropertyOfAStringObjectThatImplementsItsOwnGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnIndexPropertyOfTheObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnIndexPropertyOfAnArrayObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnNamedPropertyOfAnArgumentsObjectThatImplementsItsOwnGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnNamedPropertyOfTheStringObjectThatImplementsItsOwnGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezePIsOwnPropertyOfTheFunctionObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeTheWrtiableAttributeOfOwnDataPropertyOfOIsSetToFalseWhileOtherAttributesAreUnchanged()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-b-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeTheWrtiableAttributeOfAllOwnDataPropertyOfOIsSetToFalseWhileOtherAttributesAreUnchanged()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-b-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeTheConfigurableAttributeOfOwnDataPropertyOfOIsSetToFalseWhileOtherAttributesAreUnchanged()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeTheConfigurableAttributeOfOwnAccessorPropertyOfOIsSetToFalseWhileOtherAttributesAreUnchanged()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-c-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeTheConfigurableAttributeOfAllOwnDataPropertyOfOIsSetToFalseWhileOtherAttributesAreUnchanged()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-c-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeAllOwnPropertiesOfOAreNotWritableAndNotConfigurable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-c-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsAStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsANumberObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsADateObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeReturnedObjectIsNotExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsSealedAlready()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeOIsFrozenAlready()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.9")]
        public void ObjectFreezeTheExtensionsOfOIsPreventedAlready()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-4-3.js", false);
        }


    }
}
