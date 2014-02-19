using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_1_15 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.1.15")]
        public void DateTimeStringFormatSpecifiedDefaultValuesWillBeSetForAllOptionalFieldsMmDdMmSsAndTimeZoneWhenTheyAreAbsent()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.1/15.9.1.15/15.9.1.15-1.js", false);
        }


    }
}
