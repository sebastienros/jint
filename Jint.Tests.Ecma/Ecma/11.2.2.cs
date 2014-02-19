using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_2_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.2.2")]
        public void WhiteSpaceAndLineTerminatorBetweenNewAndNewexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void WhiteSpaceAndLineTerminatorBetweenNewAndMemberexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void OperatorNewUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfTypeNewexpressionOrTypeMemberexpressionIsNotObjectThrowTypeerror()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfTypeNewexpressionOrTypeMemberexpressionIsNotObjectThrowTypeerror2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfTypeNewexpressionOrTypeMemberexpressionIsNotObjectThrowTypeerror3()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfTypeNewexpressionOrTypeMemberexpressionIsNotObjectThrowTypeerror4()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfTypeNewexpressionOrTypeMemberexpressionIsNotObjectThrowTypeerror5()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfNewexpressionOrMemberexpressionDoesNotImplementInternalConstructMethodThrowTypeerror()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfNewexpressionOrMemberexpressionDoesNotImplementInternalConstructMethodThrowTypeerror2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfNewexpressionOrMemberexpressionDoesNotImplementInternalConstructMethodThrowTypeerror3()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfNewexpressionOrMemberexpressionDoesNotImplementInternalConstructMethodThrowTypeerror4()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.2")]
        public void IfNewexpressionOrMemberexpressionDoesNotImplementInternalConstructMethodThrowTypeerror5()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.2/S11.2.2_A4_T5.js", false);
        }


    }
}
