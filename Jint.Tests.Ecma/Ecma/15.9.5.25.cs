using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_25 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.25")]
        public void TheDatePrototypePropertyGetutcmillisecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.25/S15.9.5.25_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.25")]
        public void TheDatePrototypePropertyGetutcmillisecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.25/S15.9.5.25_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.25")]
        public void TheDatePrototypePropertyGetutcmillisecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.25/S15.9.5.25_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.25")]
        public void TheLengthPropertyOfTheGetutcmillisecondsIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.25/S15.9.5.25_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.25")]
        public void TheDatePrototypeGetutcmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.25/S15.9.5.25_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.25")]
        public void TheDatePrototypeGetutcmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.25/S15.9.5.25_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.25")]
        public void TheDatePrototypeGetutcmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.25/S15.9.5.25_A3_T3.js", false);
        }


    }
}
