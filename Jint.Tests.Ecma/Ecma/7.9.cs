using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.9")]
        public void CheckContinueStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T10.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T11.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T12.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion6()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion7()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion8()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion9()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion10()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion11()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T8.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForAutomaticSemicolonInsertion12()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A10_T9.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T10.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T11.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion6()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion7()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion8()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion9()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion10()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T8.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckIfStatementForAutomaticSemicolonInsertion11()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A11_T9.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckBreakStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckReturnStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A3.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckThrowStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A4.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckPostfixIncrementOperatorForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckPrefixIncrementOperatorForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckPostfixDecrementOperatorForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckPrefixDecrementOperatorForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckFunctionExpressionForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckFunctionExpressionForAutomaticSemicolonInsertion2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckFunctionExpressionForAutomaticSemicolonInsertion3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckFunctionExpressionForAutomaticSemicolonInsertion4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckFunctionExpressionForAutomaticSemicolonInsertion5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void SinceLineterminatorBetweenPostfixIncrementDecrementOperatorIDoAndOperandIsNotAllowedButBetweenPrefixIDoAndOperandAdmittedPostfixIDoInCombinationWithPrefixIDoAfterAutomaticSemicolonInsertionGivesValidResult()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void SinceLineterminatorBetweenPostfixIncrementDecrementOperatorIDoAndOperandIsNotAllowedButBetweenPrefixIDoAndOperandAdmittedPostfixIDoInCombinationWithPrefixIDoAfterAutomaticSemicolonInsertionGivesValidResult2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void SinceLineterminatorLtBetweenPostfixIncrementDecrementOperatorIDoAndOperandIsNotAllowedTwoIoJustAsTwoDoAndTheirCombinationBetweenTwoReferencesSeparatedByLtAfterAutomaticSemicolonInsertionLeadToSyntaxError()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.7_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void SinceLineterminatorLtBetweenPostfixIncrementDecrementOperatorIDoAndOperandIsAdmittedAdditiveSubstractOperatorASoInCombinationWithIDoSeparatedByLtOrWhiteSpacesAfterAutomaticSemicolonInsertionGivesValidResult()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void AdditiveSubstractOperatorASoInCombinationWithItselfSeparatedByLtOrWhiteSpacesAfterAutomaticSemicolonInsertionGivesValidResult()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A5.9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion6()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion7()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion8()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion9()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion10()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion11()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion12()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion13()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T10.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon6()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T5.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon7()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon8()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T7.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon9()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T8.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementUseOneSemicolon10()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.2_T9.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementDonTUseSemicolons()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementDonTUseSemicolons2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.3_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementDonTUseSemicolons3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.3_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementDonTUseSemicolons4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.3_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementDonTUseSemicolons5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.3_T5.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementDonTUseSemicolons6()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.3_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertionIfAutomaticInsertionSemicolonWouldBecomeOneOfTheTwoSemicolonsInTheHeaderOfAForStatementDonTUseSemicolons7()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.3_T7.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion14()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.4_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckForStatementForAutomaticSemicolonInsertion15()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A6.4_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion6()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion7()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T7.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion8()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckVarStatementForAutomaticSemicolonInsertion9()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A7_T9.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckEmptyStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckEmptyStatementForAutomaticSemicolonInsertion2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckEmptyStatementForAutomaticSemicolonInsertion3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckEmptyStatementForAutomaticSemicolonInsertion4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A8_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckEmptyStatementForAutomaticSemicolonInsertion5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A8_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckDoWhileStatementForAutomaticSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckDoWhileStatementForAutomaticSemicolonInsertion2()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A9_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckDoWhileStatementForAutomaticSemicolonInsertion3()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A9_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckDoWhileStatementForAutomaticSemicolonInsertion4()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A9_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckDoWhileStatementForAutomaticSemicolonInsertion5()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A9_T7.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckDoWhileStatementForAutomaticSemicolonInsertion6()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A9_T8.js", true);
        }

        [Fact]
        [Trait("Category", "7.9")]
        public void CheckDoWhileStatementForAutomaticSemicolonInsertion7()
        {
			RunTest(@"TestCases/ch07/7.9/S7.9_A9_T9.js", false);
        }


    }
}
