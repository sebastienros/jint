using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4_5_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallFSBoundargsIsUsedAsTheFormerPartOfArgumentsOfCallingTheCallInternalMethodOfFSTargetfunctionWhenFIsCalled()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs1LengthOfBoundargsIs0LengthOfExtraargsIs0AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs1LengthOfBoundargsIs0LengthOfExtraargsIs1AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs1LengthOfBoundargsIs0LengthOfExtraargsIs2AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs1LengthOfBoundargsIs1LengthOfExtraargsIs0AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs1LengthOfBoundargsIs1LengthOfExtraargsIs1AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs1LengthOfBoundargsIs2LengthOfExtraargsIs0AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallFSBoundthisIsUsedAsTheThisValueOfCallingTheCallInternalMethodOfFSTargetfunctionWhenFIsCalled()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallTheProvidedArgumentsIsUsedAsTheLatterPartOfArgumentsOfCallingTheCallInternalMethodOfFSTargetfunctionWhenFIsCalled()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs0AndWithoutBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs1AndWithoutBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs0AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs0LengthOfBoundargsIs1LengthOfExtraargsIs0AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs0LengthOfBoundargsIs0LengthOfExtraargsIs1AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5.1")]
        public void CallLengthOfParametersOfTargetIs0LengthOfBoundargsIs1LengthOfExtraargsIs1AndWithBoundthis()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-9.js", false);
        }


    }
}
