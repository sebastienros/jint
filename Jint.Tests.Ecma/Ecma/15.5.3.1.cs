using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.3.1")]
        public void TheStringHasPropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3.1")]
        public void TheStringPrototypePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3.1")]
        public void TheStringPrototypePropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3.1")]
        public void TheStringPrototypePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.1_A4.js", false);
        }


    }
}
