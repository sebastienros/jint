using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.4.3")]
        public void TheErrorPrototypeHasMessageProperty()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.3")]
        public void TheInitialValueOfErrorPrototypeMessageIs()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.3")]
        public void ErrorPrototypeMessageIsNotEnumerable()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.3/15.11.4.3-1.js", false);
        }


    }
}
