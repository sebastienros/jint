using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_8_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension010IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension010IsForbiddenInStrictMode2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension00IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtensionIsForbiddenInStrictModeAfterAHexNumberIsAssignedToAVariable()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-2gs.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension01IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtensionIsForbiddenInStrictModeAfterAHexNumberIsAssignedToAVariableFromAnEval()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-3gs.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension06IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension07IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension000IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void StrictModeOctalExtension005IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/7.8.3-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteral()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteral2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A1.2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigits()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigits2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigits3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimaldigitsExponentpart8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A2.2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteral3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteral4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigits()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigits2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigits3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart9()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart10()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart11()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart12()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart13()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart14()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart15()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralExponentpart16()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.3_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralDecimalintegerliteralDecimaldigigtsExponentpart8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A3.4_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T5.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T7.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralExponentpartIsIncorrect8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.1_T8.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void ExponentpartExponentindicator0DecimaldigitsIsAllowed8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A4.2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void DecimalliteralHexintegerliteral8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A5.1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void Hexintegerliteral0XXIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A6.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void Hexintegerliteral0XXIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A6.1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void XgIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A6.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.3")]
        public void XgIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.3/S7.8.3_A6.2_T2.js", true);
        }


    }
}
