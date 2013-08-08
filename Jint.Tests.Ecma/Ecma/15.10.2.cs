using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2")]
        public void XmlShallowParsingWithRegularExpressions()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/S15.10.2_A1_T1.js", false);
        }


    }
}
