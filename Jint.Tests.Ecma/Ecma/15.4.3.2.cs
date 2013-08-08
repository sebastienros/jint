using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayReturnTrueIfItsArgumentIsAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayReturnFalseIfItsArgumentIsNotAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayReturnTrueIfItsArgumentIsAnArrayArrayPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayReturnTrueIfItsArgumentIsAnArrayNewArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayReturnsFalseIfItsArgumentIsNotAnArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToBooleanPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToTheJsonObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToErrorObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToNumberPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToNumberObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToStringPrimitive()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToStringObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToTheMathObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToDateObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToAnObjectWithAnArrayAsThePrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToAnObjectWithArrayPrototypeAsThePrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.2")]
        public void ArrayIsarrayAppliedToAnArrayLikeObjectWithLengthAndSomeIndexedProperties()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-2-3.js", false);
        }


    }
}
