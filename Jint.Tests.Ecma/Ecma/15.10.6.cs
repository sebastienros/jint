using System.ComponentModel;
using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.6")]
        public void RegexpPrototypeIsItselfARegexp()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6")]
        public void TheValueOfTheInternalPrototypePropertyOfTheRegexpPrototypeObjectIsTheObjectPrototype()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/S15.10.6_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6")]
        public void TheValueOfTheInternalPrototypePropertyOfTheRegexpPrototypeObjectIsTheObjectPrototype2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/S15.10.6_A1_T2.js", false);
        }


    }
}
