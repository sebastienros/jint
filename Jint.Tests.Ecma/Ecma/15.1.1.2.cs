using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_1_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.1.2")]
        public void GlobalInfinityIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.2/15.1.1.2-0.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.2")]
        public void TheInitialValueOfInfinityIsNumberPositiveInfinity()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.2/S15.1.1.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.2")]
        public void TheInfinityIsNotReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.2/S15.1.1.2_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.2")]
        public void TheInfinityIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.2/S15.1.1.2_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.2")]
        public void TheInfinityIsDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.2/S15.1.1.2_A3.2.js", false);
        }


    }
}
