using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jint.Native.Array;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class PromiseTests
    {
        [Fact]
        public async Task Test()
        {
            var engine = new Engine();

            var res = await engine.Execute("var res = 0;new Promise((resolve, reject) =>resolve(12)).then(result => {res = result});").GetCompletionValueAsync();
        }

    }
}
