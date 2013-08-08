using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_6_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.6.4")]
        public void TheRegexpPrototypeTostringLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.4/S15.10.6.4_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.4")]
        public void TheLengthPropertyOfTheTostringMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.4/S15.10.6.4_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.4")]
        public void RegexpPrototypeTostringHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.4/S15.10.6.4_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.4")]
        public void RegexpPrototypeTostringCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.4/S15.10.6.4_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.4")]
        public void TheRegexpPrototypeTostringLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.4/S15.10.6.4_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.4")]
        public void TheRegexpPrototypeTostringLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.4/S15.10.6.4_A9.js", false);
        }


    }
}
