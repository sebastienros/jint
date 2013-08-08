using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.3.1")]
        public void TheArrayHasPropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.1/S15.4.3.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.1")]
        public void TheArrayPrototypePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.1/S15.4.3.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.1")]
        public void TheArrayPrototypePropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.1/S15.4.3.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.1")]
        public void TheArrayPrototypePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.1/S15.4.3.1_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3.1")]
        public void TheLengthPropertyOfArrayPrototypeIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/15.4.3.1/S15.4.3.1_A5.js", false);
        }


    }
}
