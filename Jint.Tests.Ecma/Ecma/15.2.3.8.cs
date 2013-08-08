using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealThrowsTypeerrorIfTypeOfFirstParamIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealThrowsTypeerrorIfTypeOfFirstParamIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealThrowsTypeerrorIfTypeOfFirstParamIsABooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealThrowsTypeerrorIfTypeOfFirstParamIsAStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealExtensibleOfOIsSetAsFalseEvenIfOHasNoOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealInheritedDataPropertiesAreIgnored()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealInheritedAccessorPropertiesAreIgnored()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealNonEnumerableOwnPropertyOfOIsSealed()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfABooleanObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfANumberObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfADateObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfARegexpObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfAnErrorObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfAnArgumentsObjectWhichImplementsItsOwnGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfAFunctionObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfAnArrayObjectThatUsesObjectSGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealPIsOwnPropertyOfAStringObjectWhichImplementsItsOwnGetownproperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealTheConfigurableAttributeOfOwnDataPropertyOfOIsSetFromTrueToFalseAndOtherAttributesOfThePropertyAreUnaltered()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealTheConfigurableAttributeOfOwnAccessorPropertyOfOIsSetFromTrueToFalseAndOtherAttributesOfThePropertyAreUnaltered()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealTheConfigurableAttributeOfAllOwnPropertiesOfOAreSetFromTrueToFalseAndOtherAttributesOfThePropertyAreUnaltered()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealAllOwnPropertiesOfOAreAlreadyNonConfigurable()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsAStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsANumberObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsADateObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealReturnedObjectIsNotExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsSealedAlready()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealOIsFrozenAlready()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.8")]
        public void ObjectSealTheExtensionOfOIsPreventedAlready()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-4-3.js", false);
        }


    }
}
