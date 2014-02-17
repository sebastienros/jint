using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_10 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void TheProductionCharacterescapeTEvaluatesByReturningTheCharacterU0009()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void TheProductionCharacterescapeNEvaluatesByReturningTheCharacterU000A()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void TheProductionCharacterescapeVEvaluatesByReturningTheCharacterU000B()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void TheProductionCharacterescapeFEvaluatesByReturningTheCharacterU000C()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void TheProductionCharacterescapeREvaluatesByReturningTheCharacterU000D()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A1.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeCControlletter()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeCControlletter2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A2.1_T2.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeCControlletter3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeHexescapesequenceXHexdigitHexdigit()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeHexescapesequenceXHexdigitHexdigit2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeUnicodeescapesequenceUHexdigitHexdigitHexdigitHexdigit()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeUnicodeescapesequenceUHexdigitHexdigitHexdigitHexdigit2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeUnicodeescapesequenceUHexdigitHexdigitHexdigitHexdigit3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A4.1_T3.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.10")]
        public void CharacterescapeIdentityescapesequenceSourcecharacterButNotIdentifierpart()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.10/S15.10.2.10_A5.1_T1.js", false);
        }


    }
}
