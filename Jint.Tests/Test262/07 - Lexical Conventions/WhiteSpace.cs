using Xunit;
using Xunit.Extensions;

namespace Jint.Tests.Test262
{
    public class WhiteSpace : TestBase
    {
        public WhiteSpace() : base("ch07", "7.2")
        {
        }
        
        [Theory]
        [Trait("Name", @"HORIZONTAL TAB (U+0009) between any two tokens is allowed")]
        [InlineData("S7.2_A1.1_T1.js")]
        [InlineData("S7.2_A1.1_T2.js")]
        public void HorizontalTabU0009BetweenAnyTwoTokensIsAllowed(string file)
        {
            Run(file);
        }


        [Theory]
        [Trait("Name", @"VERTICAL TAB (U+000B) between any two tokens is allowed")]
        [InlineData("S7.2_A1.2_T1.js")]
        [InlineData("S7.2_A1.2_T2.js")]
        public void VerticalTabU000BBetweenAnyTwoTokensIsAllowed(string file)
        {
            Run(file);
        }
    }
}
