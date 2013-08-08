using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_7_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.7.4")]
        public void RegexpPrototypeMultilineIsOfTypeBoolean()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.4/15.10.7.4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.4")]
        public void RegexpPrototypeMultilineIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.4/15.10.7.4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.4")]
        public void TheRegexpInstanceMultilinePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.4/S15.10.7.4_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.4")]
        public void TheRegexpInstanceMultilinePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.4/S15.10.7.4_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.4")]
        public void TheRegexpInstanceMultilinePropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.4/S15.10.7.4_A9.js", false);
        }


    }
}
