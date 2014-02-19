using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4_5_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructFSBoundargsIsUsedAsTheFormerPartOfArgumentsOfCallingTheConstructInternalMethodOfFSTargetfunctionWhenFIsCalledAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs1LengthOfBoundargsIs0LengthOfExtraargsIs1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs1LengthOfBoundargsIs0LengthOfExtraargsIs2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs1LengthOfBoundargsIs1LengthOfExtraargsIs0()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs1LengthOfBoundargsIs1LengthOfExtraargsIs1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs1LengthOfBoundargsIs2LengthOfExtraargsIs0()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructTheProvidedArgumentsIsUsedAsTheLatterPartOfArgumentsOfCallingTheConstructInternalMethodOfFSTargetfunctionWhenFIsCalledAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs0AndWithoutBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs1AndWithoutBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs0AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs0LengthOfBoundargsIs1LengthOfExtraargsIs0()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs0LengthOfBoundargsIs1LengthOfExtraargsIs1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.2")]
        public void ConstructLengthOfParametersOfTargetIs1LengthOfBoundargsIs0LengthOfExtraargsIs0()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-9.js", false);
        }


    }
}
