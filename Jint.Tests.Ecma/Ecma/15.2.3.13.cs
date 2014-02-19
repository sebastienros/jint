using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_13 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ANewlyCreatedObjectUsingTheObjectContructorHasItsExtensiblePropertySetToTrueByDefault15221Step8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleThrowsTypeerrorIfOIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleThrowsTypeerrorIfOIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleThrowsTypeerrorIfOIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleThrowsTypeerrorIfOIsAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsGlobal()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsRegexp()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsError()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsJson()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsFunctionConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsFunctionPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsArrayPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsStringPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsBooleanPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsNumberPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsDatePrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsRegexpPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void FunctionConstructorFunctionPrototypeArrayPrototypeStringPrototypeBooleanPrototypeNumberPrototypeDatePrototypeRegexpPrototypeErrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueIfOIsExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsFalseIfOIsNotExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueIfOIsExtensibleAndHasAPrototypeThatIsExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueIfOIsExtensibleAndHasAPrototypeThatIsNotExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsFalseIfOIsNotExtensibleAndHasAPrototypeThatIsExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsFalseIfOIsNotExtensibleAndHasAPrototypeThatIsNotExtensible()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsBoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsMath()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.13")]
        public void ObjectIsextensibleReturnsTrueForAllBuiltInObjectsDate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-9.js", false);
        }


    }
}
