using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_7_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.7.1")]
        public void RegexpPrototypeSourceIsOfTypeString()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.1/15.10.7.1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.1")]
        public void RegexpPrototypeSourceIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.1/15.10.7.1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.1")]
        public void TheRegexpInstanceSourcePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.1/S15.10.7.1_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.1")]
        public void TheRegexpInstanceSourcePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.1/S15.10.7.1_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.1")]
        public void TheRegexpInstanceSourcePropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.1/S15.10.7.1_A9.js", false);
        }


    }
}
