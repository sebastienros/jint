using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_10 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.10")]
        public void TheDatePrototypePropertyGetfullyearHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.10/S15.9.5.10_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.10")]
        public void TheDatePrototypePropertyGetfullyearHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.10/S15.9.5.10_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.10")]
        public void TheDatePrototypePropertyGetfullyearHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.10/S15.9.5.10_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.10")]
        public void TheLengthPropertyOfTheGetfullyearIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.10/S15.9.5.10_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.10")]
        public void TheDatePrototypeGetfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.10/S15.9.5.10_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.10")]
        public void TheDatePrototypeGetfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.10/S15.9.5.10_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.10")]
        public void TheDatePrototypeGetfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.10/S15.9.5.10_A3_T3.js", false);
        }


    }
}
