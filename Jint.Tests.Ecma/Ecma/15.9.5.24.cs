using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_24 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.24")]
        public void TheDatePrototypePropertyGetmillisecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.24/S15.9.5.24_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.24")]
        public void TheDatePrototypePropertyGetmillisecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.24/S15.9.5.24_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.24")]
        public void TheDatePrototypePropertyGetmillisecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.24/S15.9.5.24_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.24")]
        public void TheLengthPropertyOfTheGetmillisecondsIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.24/S15.9.5.24_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.24")]
        public void TheDatePrototypeGetmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.24/S15.9.5.24_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.24")]
        public void TheDatePrototypeGetmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.24/S15.9.5.24_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.24")]
        public void TheDatePrototypeGetmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.24/S15.9.5.24_A3_T3.js", false);
        }


    }
}
