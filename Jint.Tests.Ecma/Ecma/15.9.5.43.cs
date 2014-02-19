using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_43 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringRangeerrorIsNotThrownWhenValueOfDateIsDate19700999999990001TheTimeZoneIsUtc0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringRangeerrorIsNotThrownWhenValueOfDateIsDate197001000000010001TheTimeZoneIsUtc0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringRangeerrorIsNotThrownWhenValueOfDateIsDate197001000000010000TheTimeZoneIsUtc0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringRangeerrorIsThrownWhenValueOfDateIsDate197001000000010001TheTimeZoneIsUtc0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringWhenValueOfYearIsInfinityDatePrototypeToisostringThrowTheRangeerror()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringValueOfYearIsInfinityDatePrototypeToisostringThrowTheRangeerror()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringWhenThisIsAStringObjectThatValueFormatIsYyyyMmDdthhMmSsSsszDatePrototypeToisostringThrowTheTypeerror()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringMustExistAsAFunctionTaking0Parameters()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringFormatOfReturnedStringIsYyyyMmDdthhMmSsSsszTheTimeZoneIsUtc0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringTheReturnedStringIsTheUtcTimeZone0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringTypeerrorIsThrownWhenThisIsAnyOtherObjectsInsteadOfDateObject()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringTypeerrorIsThrownWhenThisIsAnyPrimitiveValues()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringRangeerrorIsThrownWhenValueOfDateIsDate19700999999990001TheTimeZoneIsUtc0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.43")]
        public void DatePrototypeToisostringRangeerrorIsNotThrownWhenValueOfDateIsDate19700999999990000TheTimeZoneIsUtc0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-9.js", false);
        }


    }
}
