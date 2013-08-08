using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_2_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void IsnanAppliesTonumberToItsArgumentThenReturnTrueIfTheResultIsNanAndOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void IsnanAppliesTonumberToItsArgumentThenReturnTrueIfTheResultIsNanAndOtherwiseReturnFalse2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void TheLengthPropertyOfIsnanHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A2.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void TheLengthPropertyOfIsnanHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A2.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void TheLengthPropertyOfIsnanHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A2.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void TheLengthPropertyOfIsnanIs1()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A2.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void TheIsnanPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A2.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void TheIsnanPropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A2.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.4")]
        public void TheIsnanPropertyCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.4/S15.1.2.4_A2.7.js", false);
        }


    }
}
