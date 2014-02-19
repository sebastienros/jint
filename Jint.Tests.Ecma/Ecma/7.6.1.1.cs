using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_6_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheBreakTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.1.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheForTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.10.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheFunctionTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.11.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheIfTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.12.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheInTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.13.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheInstanceofTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.14.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheNewTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.15.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheReturnTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.16.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheSwitchTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.17.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheThisTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.18.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheThrowTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.19.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheCaseTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.2.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheTryTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.20.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheTypeofTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.21.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheVarTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.22.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheVoidTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.23.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheWhileTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.24.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheWithTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.25.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheCatchTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.3.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheContinueTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.4.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheDefaultTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.5.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheDeleteTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.6.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheDoTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.7.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheElseTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.8.js", true);
        }

        [Fact]
        [Trait("Category", "7.6.1.1")]
        public void TheFinallyTokenCanNotBeUsedAsIdentifier()
        {
			RunTest(@"TestCases/ch07/7.6/7.6.1/7.6.1.1/S7.6.1.1_A1.9.js", true);
        }


    }
}
