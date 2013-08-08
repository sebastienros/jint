using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_6_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.6.1")]
        public void APropertyCanHaveAttributeReadonlyLikeEInMath()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.1/S8.6.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.1")]
        public void APropertyCanHaveAttributeDontenumLikeAllPropertiesOfNumber()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.1/S8.6.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "8.6.1")]
        public void APropertyCanHaveAttributeDontdeleteLikeNanPropertieOfNumberObject()
        {
			RunTest(@"TestCases/ch08/8.6/8.6.1/S8.6.1_A3.js", false);
        }


    }
}
