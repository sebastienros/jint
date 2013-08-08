using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_10 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsThrowsTypeerrorIfOIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsThrowsTypeerrorIfOIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsThrowsTypeerrorIfOIsABooleanPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsThrowsTypeerrorIfOIsAStringPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsRepeatedCallsToPreventextensionsHaveNoSideEffects()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void TheEffectOfPreventextentionsMustBeTestableByCallingIsextensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsObjectIsextensibleArgReturnsFalseIfArgIsTheReturnedObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoTheReturnedObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoAStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoANumberObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoADateObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoTheReturnedObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoAnErrorObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsNamedPropertiesCannotBeAddedIntoAnArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsPropertiesCanStillBeDeletedAfterExtensionsHaveBeenPrevented()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsPropertiesCanStillBeReassignedAfterExtensionsHaveBeenPrevented()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsExtensibleFalseOnAPrototypeDoesnTPreventAddingPropertiesToAnInstanceThatInheritsFromThatPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoAStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoAStringObject2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoABooleanObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoANumberObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoADateObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.10")]
        public void ObjectPreventextensionsIndexedPropertiesCannotBeAddedIntoARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-9.js", false);
        }


    }
}
