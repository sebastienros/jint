using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_15 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.15")]
        public void TheDatePrototypePropertyGetutcdateHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.15/S15.9.5.15_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.15")]
        public void TheDatePrototypePropertyGetutcdateHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.15/S15.9.5.15_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.15")]
        public void TheDatePrototypePropertyGetutcdateHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.15/S15.9.5.15_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.15")]
        public void TheLengthPropertyOfTheGetutcdateIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.15/S15.9.5.15_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.15")]
        public void TheDatePrototypeGetutcdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.15/S15.9.5.15_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.15")]
        public void TheDatePrototypeGetutcdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.15/S15.9.5.15_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.15")]
        public void TheDatePrototypeGetutcdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.15/S15.9.5.15_A3_T3.js", false);
        }


    }
}
