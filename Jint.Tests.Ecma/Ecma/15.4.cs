using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo2321()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23212()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23213()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23214()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23215()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23216()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23217()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23218()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo23219()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.4")]
        public void APropertyNamePInTheFormOfAStringValueIsAnArrayIndexIfAndOnlyIfTostringTouint32PIsEqualToPAndTouint32PIsNotEqualTo232110()
        {
			RunTest(@"TestCases/ch15/15.4/S15.4_A1.1_T9.js", false);
        }


    }
}
