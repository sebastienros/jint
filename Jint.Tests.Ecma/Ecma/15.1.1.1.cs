using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.1.1")]
        public void GlobalNanIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.1/15.1.1.1-0.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.1")]
        public void TheInitialValueOfNanIsNan()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.1/S15.1.1.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.1")]
        public void TheNanIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.1/S15.1.1.1_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.1")]
        public void TheNanIsDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.1/S15.1.1.1_A3.2.js", false);
        }


    }
}
