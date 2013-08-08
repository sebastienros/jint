using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11")]
        public void ErrorConversionerrorHasBeenRemovedFromIe9StandardMode()
        {
			RunTest(@"TestCases/ch15/15.11/15.11-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11")]
        public void ErrorRegexperrorHasBeenRemovedFromIe9StandardMode()
        {
			RunTest(@"TestCases/ch15/15.11/15.11-2.js", false);
        }


    }
}
