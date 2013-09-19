using Xunit;

namespace Jint.Tests.Ecma
{
    [Trait("Category", "Pass")]
    public class Test_6_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "6.1")]
        public void TestForHandlingOfSupplementaryCharacters()
        {
			RunTest(@"TestCases/ch06/6.1.js", false);
        }


    }
}
