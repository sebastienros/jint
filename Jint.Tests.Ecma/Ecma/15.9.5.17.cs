using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_17 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.17")]
        public void TheDatePrototypePropertyGetutcdayHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.17/S15.9.5.17_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.17")]
        public void TheDatePrototypePropertyGetutcdayHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.17/S15.9.5.17_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.17")]
        public void TheDatePrototypePropertyGetutcdayHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.17/S15.9.5.17_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.17")]
        public void TheLengthPropertyOfTheGetutcdayIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.17/S15.9.5.17_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.17")]
        public void TheDatePrototypeGetutcdayPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.17/S15.9.5.17_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.17")]
        public void TheDatePrototypeGetutcdayPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.17/S15.9.5.17_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.17")]
        public void TheDatePrototypeGetutcdayPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.17/S15.9.5.17_A3_T3.js", false);
        }


    }
}
