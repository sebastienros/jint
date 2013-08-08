using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_41 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.41")]
        public void TheDatePrototypePropertySetutcfullyearHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.41/S15.9.5.41_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.41")]
        public void TheDatePrototypePropertySetutcfullyearHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.41/S15.9.5.41_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.41")]
        public void TheDatePrototypePropertySetutcfullyearHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.41/S15.9.5.41_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.41")]
        public void TheLengthPropertyOfTheSetutcfullyearIs3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.41/S15.9.5.41_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.41")]
        public void TheDatePrototypeSetutcfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.41/S15.9.5.41_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.41")]
        public void TheDatePrototypeSetutcfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.41/S15.9.5.41_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.41")]
        public void TheDatePrototypeSetutcfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.41/S15.9.5.41_A3_T3.js", false);
        }


    }
}
