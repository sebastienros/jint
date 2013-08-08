using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenTypeerrorIsThrownWhenTheFirstParamOIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenTypeerrorIsThrownWhenTheFirstParamOIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenTypeerrorIsThrownWhenTheFirstParamOIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenTypeerrorIsThrownWhenTheFirstParamOIsAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenAppliesToDenseArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenAppliesToSparseArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenAppliesToNonArrayObjectWhichContainsIndexNamedProperties()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenInheritedDataPropertyIsNotConsideredIntoTheForEachLoop()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenInheritedAccessorPropertyIsNotConsideredIntoTheForEachLoop()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenOIsTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenOIsAStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenOIsAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenOIsAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenPIsOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseIfOContainsOwnWritableDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-b-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseIfOContainsOwnConfigurableDataProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseIfOContainsOwnConfigurableAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-c-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsGlobal()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsBoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsBooleanPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsNumberPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsMath()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsDate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsDatePrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsRegexp()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsRegexpPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsError()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsErrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsEvalerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsRangeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsReferenceerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsSyntaxerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsTypeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsUrierror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsJson()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsTrueWhenAllOwnPropertiesOfOAreNotWritableAndNotConfigurableAndOIsNotExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsObjectPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsFunctionPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsArrayPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseForAllBuiltInObjectsStringPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.12")]
        public void ObjectIsfrozenReturnsFalseIfExtensibleIsTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-4-1.js", false);
        }


    }
}
