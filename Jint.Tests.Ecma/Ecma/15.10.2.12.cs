using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_12 : EcmaTest
    {
        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfCharactersContainingTheCharactersThatAreOnTheRightHandSideOfTheWhitespace72OrLineterminator73Productions()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A1_T1.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfCharactersContainingTheCharactersThatAreOnTheRightHandSideOfTheWhitespace72OrLineterminator73Productions2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfCharactersContainingTheCharactersThatAreOnTheRightHandSideOfTheWhitespace72OrLineterminator73Productions3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfCharactersContainingTheCharactersThatAreOnTheRightHandSideOfTheWhitespace72OrLineterminator73Productions4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfCharactersContainingTheCharactersThatAreOnTheRightHandSideOfTheWhitespace72OrLineterminator73Productions5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A1_T5.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeS()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A2_T1.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeS2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeS3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeS4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeSEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeS5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A2_T5.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfCharactersContainingTheSixtyThreeCharactersAZAZ09()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfCharactersContainingTheSixtyThreeCharactersAZAZ092()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfCharactersContainingTheSixtyThreeCharactersAZAZ093()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfCharactersContainingTheSixtyThreeCharactersAZAZ094()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfCharactersContainingTheSixtyThreeCharactersAZAZ095()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A3_T5.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeW()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeW2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeW3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeW4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeWEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeW5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheTenElementSetOfCharactersContainingTheCharacters0Through9Inclusive()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheTenElementSetOfCharactersContainingTheCharacters0Through9Inclusive2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheTenElementSetOfCharactersContainingTheCharacters0Through9Inclusive3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheTenElementSetOfCharactersContainingTheCharacters0Through9Inclusive4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeD()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeD2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeD3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.12")]
        public void TheProductionCharacterclassescapeDEvaluatesByReturningTheSetOfAllCharactersNotIncludedInTheSetReturnedByCharacterclassescapeD4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.12/S15.10.2.12_A6_T4.js", false);
        }


    }
}
