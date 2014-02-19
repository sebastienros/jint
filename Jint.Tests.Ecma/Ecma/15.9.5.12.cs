using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.12")]
        public void TheDatePrototypePropertyGetmonthHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.12/S15.9.5.12_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.12")]
        public void TheDatePrototypePropertyGetmonthHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.12/S15.9.5.12_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.12")]
        public void TheDatePrototypePropertyGetmonthHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.12/S15.9.5.12_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.12")]
        public void TheLengthPropertyOfTheGetmonthIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.12/S15.9.5.12_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.12")]
        public void TheDatePrototypeGetmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.12/S15.9.5.12_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.12")]
        public void TheDatePrototypeGetmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.12/S15.9.5.12_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.12")]
        public void TheDatePrototypeGetmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.12/S15.9.5.12_A3_T3.js", false);
        }


    }
}
