using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.2")]
        public void TheNullTypeHasOneValueCalledNull()
        {
			RunTest(@"TestCases/ch08/8.2/S8.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.2")]
        public void TheNullTypeHasOneValueCalledNull2()
        {
			RunTest(@"TestCases/ch08/8.2/S8.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.2")]
        public void TheNullIsResrvedWord()
        {
			RunTest(@"TestCases/ch08/8.2/S8.2_A2.js", true);
        }

        [Fact]
        [Trait("Category", "8.2")]
        public void ForTheKeywordNullTheTypeofOperatorReturnsTheObjectSeeAlsoHttpDeveloperMozillaOrgEnDocsCoreJavascript15ReferenceOperatorsSpecialOperatorsTypeofOperatorAndHttpBugsEcmascriptOrgTicket250ForExample()
        {
			RunTest(@"TestCases/ch08/8.2/S8.2_A3.js", false);
        }


    }
}
