using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.3")]
        public void TheProductionEmptystatementIsEvaluatedAsFollowsReturnNormalEmptyEmpty()
        {
			RunTest(@"TestCases/ch12/12.3/S12.3_A1.js", false);
        }


    }
}
