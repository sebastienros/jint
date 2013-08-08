using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.4.2")]
        public void TheErrorPrototypeHasNameProperty()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.2")]
        public void TheInitialValueOfErrorPrototypeNameIsError()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.2")]
        public void ErrorPrototypeNameIsNotEnumerable()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.2/15.11.4.2-1.js", false);
        }


    }
}
