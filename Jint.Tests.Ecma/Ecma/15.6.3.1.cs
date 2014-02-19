using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.3.1")]
        public void TheInitialValueOfBooleanPrototypeIsTheBooleanPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.3/15.6.3.1/S15.6.3.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.3.1")]
        public void BooleanPrototypeHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.3/15.6.3.1/S15.6.3.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.3.1")]
        public void BooleanPrototypeHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.3/15.6.3.1/S15.6.3.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.3.1")]
        public void BooleanPrototypeHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.3/15.6.3.1/S15.6.3.1_A4.js", false);
        }


    }
}
