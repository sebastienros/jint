using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests
{
    public class CatchingClrExceptionsTest
    {
        [Fact]
        public void ItShouldBePossibleToCatchClrExceptionsInJavaScript()
        {
            var engine = new Engine();
            engine.SetValue("error", new Action(() => { throw new ArgumentOutOfRangeException("x"); }));

            engine.Execute(@"
function run(){
    try {
        error();
        return false;
    } 
    catch(e) {
        return true;
    }
}
");

            var result = engine.Invoke("run");

            Assert.Equal(result.AsBoolean(), true);
        }
    }
}
