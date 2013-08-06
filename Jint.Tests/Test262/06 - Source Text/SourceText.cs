using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace Jint.Tests.Test262
{
    public class SourceText : TestBase
    {
        public SourceText()
            : base("ch06")
        {
        }

        [Theory]
        [Trait("Description", @"Test for handling of supplementary characters")]
        [InlineData("6.1.js")]
        public void TestForHandlingSupplimentaryCharacters(string file)
        {
            Run(file);
        }
    }
}
