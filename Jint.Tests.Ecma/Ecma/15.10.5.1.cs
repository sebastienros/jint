using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_5_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.5.1")]
        public void TheRegexpHasPropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.5/S15.10.5.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.5.1")]
        public void TheRegexpPrototypePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.5/S15.10.5.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.5.1")]
        public void TheRegexpPrototypePropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.5/S15.10.5.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.5.1")]
        public void TheRegexpPrototypePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.5/S15.10.5.1_A4.js", false);
        }


    }
}
