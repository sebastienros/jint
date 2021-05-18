using Xunit;

namespace Jint.Tests.Runtime
{
    public class JsonTests
    {
        [Fact]
        public void CanParseTabsInProperties()
        {
             var engine = new Engine();
             const string script = @"JSON.parse(""{\""abc\\tdef\"": \""42\""}"");";
             var obj = engine.Evaluate(script).AsObject();
             Assert.True(obj.HasOwnProperty("abc\tdef"));
        }
    }
}