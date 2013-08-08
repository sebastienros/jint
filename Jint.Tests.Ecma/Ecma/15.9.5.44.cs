using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_44 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.44")]
        public void DatePrototypeTojsonMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.44/15.9.5.44-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.44")]
        public void DatePrototypeTojsonMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.44/15.9.5.44-0-2.js", false);
        }


    }
}
