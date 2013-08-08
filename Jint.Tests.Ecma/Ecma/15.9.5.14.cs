using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.14")]
        public void TheDatePrototypePropertyGetdateHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.14")]
        public void TheDatePrototypePropertyGetdateHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.14")]
        public void TheDatePrototypePropertyGetdateHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.14")]
        public void TheLengthPropertyOfTheGetdateIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.14")]
        public void TheDatePrototypeGetdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.14")]
        public void TheDatePrototypeGetdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.14")]
        public void TheDatePrototypeGetdatePropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A3_T3.js", false);
        }


    }
}
