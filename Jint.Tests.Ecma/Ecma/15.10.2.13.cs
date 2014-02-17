using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_13 : EcmaTest
    {
        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T16.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T17.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse15()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse16()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassLookaheadNotinClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanFalse17()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A1_T9.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T1.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T7.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.13")]
        public void TheProductionCharacterclassClassrangesEvaluatesByEvaluatingClassrangesToObtainACharsetAndReturningThatCharsetAndTheBooleanTrue8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void InsideACharacterclassBMeansTheBackspaceCharacter()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void InsideACharacterclassBMeansTheBackspaceCharacter2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void InsideACharacterclassBMeansTheBackspaceCharacter3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.13")]
        public void InsideACharacterclassBMeansTheBackspaceCharacter4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.13/S15.10.2.13_A3_T4.js", false);
        }


    }
}
