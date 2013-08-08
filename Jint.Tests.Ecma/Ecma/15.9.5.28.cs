using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_28 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.28")]
        public void TheDatePrototypePropertySetmillisecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.28/S15.9.5.28_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.28")]
        public void TheDatePrototypePropertySetmillisecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.28/S15.9.5.28_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.28")]
        public void TheDatePrototypePropertySetmillisecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.28/S15.9.5.28_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.28")]
        public void TheLengthPropertyOfTheSetmillisecondsIs1()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.28/S15.9.5.28_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.28")]
        public void TheDatePrototypeSetmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.28/S15.9.5.28_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.28")]
        public void TheDatePrototypeSetmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.28/S15.9.5.28_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.28")]
        public void TheDatePrototypeSetmillisecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.28/S15.9.5.28_A3_T3.js", false);
        }


    }
}
