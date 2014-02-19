using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_23 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.23")]
        public void TheDatePrototypePropertyGetutcsecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.23/S15.9.5.23_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.23")]
        public void TheDatePrototypePropertyGetutcsecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.23/S15.9.5.23_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.23")]
        public void TheDatePrototypePropertyGetutcsecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.23/S15.9.5.23_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.23")]
        public void TheLengthPropertyOfTheGetutcsecondsIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.23/S15.9.5.23_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.23")]
        public void TheDatePrototypeGetutcsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.23/S15.9.5.23_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.23")]
        public void TheDatePrototypeGetutcsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.23/S15.9.5.23_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.23")]
        public void TheDatePrototypeGetutcsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.23/S15.9.5.23_A3_T3.js", false);
        }


    }
}
