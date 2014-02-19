using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_2_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void IsfiniteAppliesTonumberToItsArgumentThenReturnFalseIfTheResultIsNanInfinityInfinityAndOtherwiseReturnTrue()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void IsfiniteAppliesTonumberToItsArgumentThenReturnFalseIfTheResultIsNanInfinityInfinityAndOtherwiseReturnTrue2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void TheLengthPropertyOfIsfiniteHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A2.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void TheLengthPropertyOfIsfiniteHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A2.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void TheLengthPropertyOfIsfiniteHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A2.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void TheLengthPropertyOfIsfiniteIs1()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A2.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void TheIsfinitePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A2.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void TheIsfinitePropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A2.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.5")]
        public void TheIsfinitePropertyCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.5/S15.1.2.5_A2.7.js", false);
        }


    }
}
