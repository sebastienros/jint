using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsGlobal()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsBoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsBooleanPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsNumberPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsMath()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsDate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsDatePrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsRegexp()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsRegexpPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsError()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsErrorPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsEvalerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsRangeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsReferenceerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsSyntaxerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsTypeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsUrierror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsJson()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsObjectPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsFunctionPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsArrayPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.11")]
        public void ObjectIssealedReturnsFalseForAllBuiltInObjectsStringPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-9.js", false);
        }


    }
}
