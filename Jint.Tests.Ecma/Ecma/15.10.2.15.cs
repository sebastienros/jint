using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_15 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void PatternSyntaxerrorWasThrownWhenADoesNotContainExactlyOneCharacter151025Step3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void PatternSyntaxerrorWasThrownWhenBDoesNotContainExactlyOneCharacter151025Step3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void PatternSyntaxerrorWasThrownWhenOneCharacterInCharsetAGreaterThanOneCharacterInCharsetB1510215CharacterrangeStep6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T20.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T21.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException15()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T22.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException16()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T23.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException17()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T24.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException18()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T25.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException19()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T26.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException20()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T27.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException21()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T28.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException22()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T29.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException23()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException24()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T30.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException25()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T31.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException26()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T32.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException27()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T33.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException28()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T34.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException29()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T35.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException30()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T36.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException31()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T37.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException32()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T38.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException33()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T39.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException34()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException35()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T40.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException36()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T41.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException37()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException38()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException39()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException40()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.15")]
        public void TheInternalHelperFunctionCharacterrangeTakesTwoCharsetParametersAAndBAndPerformsTheFollowingIfADoesNotContainExactlyOneCharacterOrBDoesNotContainExactlyOneCharacterThenThrowASyntaxerrorException41()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.15/S15.10.2.15_A1_T9.js", false);
        }


    }
}
