using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.11")]
        public void DecimalescapeDecimalintegerliteralLookaheadNotInDecimaldigit()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.11/S15.10.2.11_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.11")]
        public void DecimalescapeDecimalintegerliteralLookaheadNotInDecimaldigit2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.11/S15.10.2.11_A1_T4.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.11")]
        public void DecimalescapeDecimalintegerliteralLookaheadNotInDecimaldigit3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.11/S15.10.2.11_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.11")]
        public void DecimalescapeDecimalintegerliteralLookaheadNotInDecimaldigit4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.11/S15.10.2.11_A1_T6.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.11")]
        public void DecimalescapeDecimalintegerliteralLookaheadNotInDecimaldigit5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.11/S15.10.2.11_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.11")]
        public void DecimalescapeDecimalintegerliteralLookaheadNotInDecimaldigit6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.11/S15.10.2.11_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.11")]
        public void DecimalescapeDecimalintegerliteralLookaheadNotInDecimaldigit7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.11/S15.10.2.11_A1_T9.js", false);
        }


    }
}
