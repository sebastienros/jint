using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_7_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.7.5")]
        public void RegexpPrototypeLastindexIsOfTypeNumber()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.5/15.10.7.5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.5")]
        public void RegexpPrototypeLastindexIsADataPropertyWithSpecifiedAttributeValues()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.5/15.10.7.5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.5")]
        public void TheRegexpInstanceLastindexPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.5/S15.10.7.5_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.5")]
        public void TheRegexpInstanceLastindexPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.5/S15.10.7.5_A9.js", false);
        }


    }
}
