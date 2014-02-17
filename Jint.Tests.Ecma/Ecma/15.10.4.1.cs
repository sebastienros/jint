using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenLetPBeThePatternUsedToConstructRAndLetFBeTheFlagsUsedToConstructR()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenLetPBeThePatternUsedToConstructRAndLetFBeTheFlagsUsedToConstructR2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenLetPBeThePatternUsedToConstructRAndLetFBeTheFlagsUsedToConstructR3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenLetPBeThePatternUsedToConstructRAndLetFBeTheFlagsUsedToConstructR4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenLetPBeThePatternUsedToConstructRAndLetFBeTheFlagsUsedToConstructR5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsNotUndefinedThenThrowATypeerrorException()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsNotUndefinedThenThrowATypeerrorException2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTheEmptyStringIfPatternIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTheEmptyStringIfPatternIsUndefined2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTheEmptyStringIfPatternIsUndefined3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTheEmptyStringIfPatternIsUndefined4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTheEmptyStringIfPatternIsUndefined5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetFBeTheEmptyStringIfFlagsIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetFBeTheEmptyStringIfFlagsIsUndefined2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetFBeTheEmptyStringIfFlagsIsUndefined3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetFBeTheEmptyStringIfFlagsIsUndefined4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetFBeTheEmptyStringIfFlagsIsUndefined5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfFContainsAnyCharacterOtherThanGIOrMOrIfItContainsTheSameOneMoreThanOnceThenThrowASyntaxerrorException8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A5_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToRegexp()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalRegexpPrototypeObjectTheOneThatIsTheInitialValueOfRegexpPrototype()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalRegexpPrototypeObjectTheOneThatIsTheInitialValueOfRegexpPrototype2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T13.js", false);
        }

        [Fact(Skip="Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void LetPBeTostringPatternAndLetFBeTostringFlags13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A8_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPSCharactersDoNotHaveTheFormPatternThenThrowASyntaxerrorException()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPSCharactersDoNotHaveTheFormPatternThenThrowASyntaxerrorException2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A9_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void IfPSCharactersDoNotHaveTheFormPatternThenThrowASyntaxerrorException3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/S15.10.4.1_A9_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void RegexpTheThrownErrorIsTypeerrorInsteadOfRegexperrorWhenPatternIsAnObjectWhoseClassPropertyIsRegexpAndFlagsIsNotUndefined()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void RegexpTheThrownErrorIsSyntaxerrorInsteadOfRegexperrorWhenTheCharactersOfPDoNotHaveTheSyntacticFormPattern()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void RegexpTheThrownErrorIsSyntaxerrorInsteadOfRegexperrorWhenFContainsAnyCharacterOtherThanGIOrM()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.4.1")]
        public void RegexpTheSyntaxerrorIsNotThrownWhenFlagsIsGim()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-4.js", false);
        }


    }
}
