using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_14_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.14.4")]
        public void NonWritablePropertyOnAPrototypeWrittenTo()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.4/8.14.4-8-b_1.js", false);
        }

        [Fact]
        [Trait("Category", "8.14.4")]
        public void NonWritablePropertyOnAPrototypeWrittenToInStrictMode()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.4/8.14.4-8-b_2.js", false);
        }


    }
}
