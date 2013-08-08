using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.3.1")]
        public void ErrorPrototypePropertyHasTheAttributesDontdelete()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.3/S15.11.3.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.3.1")]
        public void ErrorPrototypePropertyHasTheAttributesDontenum()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.3/S15.11.3.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.3.1")]
        public void ErrorPrototypePropertyHasTheAttributesReadonly()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.3/S15.11.3.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.3.1")]
        public void TheErrorHasPropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.3/S15.11.3.1_A4_T1.js", false);
        }


    }
}
