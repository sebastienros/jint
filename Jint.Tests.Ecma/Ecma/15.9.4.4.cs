using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.4.4")]
        public void DateNowMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.4")]
        public void DateNowMustExistAsAFunctionTaking0Parameters()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.4")]
        public void DateNowMustExistAsAFunction2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4.4")]
        public void DateNowReturnsNumber()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-4.js", false);
        }


    }
}
