using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofMustTake1Parameter()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofThrowsTypeerrorIfOIsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofThrowsTypeerrorIfOIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofThrowsTypeerrorIfOIsAString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofThrowsTypeerrorIfTypeOfFirstParamIsNotObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterBoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterRegexp()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterError()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterEvalerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterRangeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterReferenceerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterSyntaxerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterTypeerror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterUrierror()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterJson()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterObjectObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void LetXBeTheReturnValueFromGetprototypeofWhenCalledOnDThenXIsprototypeofDMustBeTrue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterArrayObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterStringObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterBooleanObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterNumberObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterDateObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterRegexpObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterErrorObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterTheArgumentsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterTheGlobalObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsNull()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterFunction()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterArray()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterMath()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.2")]
        public void ObjectGetprototypeofReturnsThePrototypeOfItsParameterDate()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-9.js", false);
        }


    }
}
