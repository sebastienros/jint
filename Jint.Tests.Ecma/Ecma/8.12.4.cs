using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.4")]
        public void IfThePropertyHasTheReadonlyAttributeCanputPReturnFalse()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.4/S8.12.4_A1.js", false);
        }


    }
}
