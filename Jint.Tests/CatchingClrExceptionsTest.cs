using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        return e;
    }
}
");

            var result = engine.Invoke("run");

            Assert.IsAssignableFrom<ArgumentOutOfRangeException>(result.ToObject());
        }

        [Fact]
        public void UncaughtClrExceptionsIsThrown()
        {
            var engine = new Engine();
            engine.SetValue("error", new Action(() => { throw new ArgumentOutOfRangeException("x"); }));

            engine.Execute(@"
function run(){
   error();
}
");
            try
            {
                engine.Invoke("run");
                Assert.False(true);
            }
            catch (Exception e)
            {
                Assert.IsAssignableFrom<ArgumentOutOfRangeException>(e);                
            }            
        }
    }
}
