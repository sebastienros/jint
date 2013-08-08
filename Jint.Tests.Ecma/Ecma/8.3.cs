using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.3")]
        public void TheBooleanTypeHaveTwoValuesCalledTrueAndFalse()
        {
			RunTest(@"TestCases/ch08/8.3/S8.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.3")]
        public void TheBooleanTypeHaveTwoValuesCalledTrueAndFalse2()
        {
			RunTest(@"TestCases/ch08/8.3/S8.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.3")]
        public void TheTrueIsReservedWord()
        {
			RunTest(@"TestCases/ch08/8.3/S8.3_A2.1.js", true);
        }

        [Fact]
        [Trait("Category", "8.3")]
        public void TheFalseIsReservedWord()
        {
			RunTest(@"TestCases/ch08/8.3/S8.3_A2.2.js", true);
        }

        [Fact]
        [Trait("Category", "8.3")]
        public void ApplaingNegationToBooleanWorksWell()
        {
			RunTest(@"TestCases/ch08/8.3/S8.3_A3.js", false);
        }


    }
}
