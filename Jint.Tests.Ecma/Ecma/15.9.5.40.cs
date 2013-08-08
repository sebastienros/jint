using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_40 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void DatePrototypeSetfullyearDatePrototypeIsItselfAnInstanceOfDate()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/15.9.5.40_1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void TheDatePrototypePropertySetfullyearHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/S15.9.5.40_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void TheDatePrototypePropertySetfullyearHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/S15.9.5.40_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void TheDatePrototypePropertySetfullyearHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/S15.9.5.40_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void TheLengthPropertyOfTheSetfullyearIs3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/S15.9.5.40_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void TheDatePrototypeSetfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/S15.9.5.40_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void TheDatePrototypeSetfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/S15.9.5.40_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.40")]
        public void TheDatePrototypeSetfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.40/S15.9.5.40_A3_T3.js", false);
        }


    }
}
