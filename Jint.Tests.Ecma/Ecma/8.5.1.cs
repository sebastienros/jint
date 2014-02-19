using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_5_1 : EcmaTest
    {
        [Fact(Skip = "C# can't distinguish 1.797693134862315808e+308 and 1.797693134862315708145274237317e+308")]
        [Trait("Category", "8.5.1")]
        public void ValidNumberRanges()
        {
			RunTest(@"TestCases/ch08/8.5/8.5.1.js", false);
        }


    }
}
