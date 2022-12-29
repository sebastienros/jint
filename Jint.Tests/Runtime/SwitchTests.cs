namespace Jint.Tests.Runtime;

public class JintFailureTest
{
    [Fact]
    public void ShouldHandleCaseBlockLexicalScopeCorrectly()
    {
        var engine = new Engine();
        engine.SetValue("switchVal", 1);
        engine.SetValue("getCoffee", new Func<string>(() => "coffee"));

        engine.Execute(@"
        function myFunc() {
            switch(switchVal) {
                case 0:
                    const text = getCoffee();
                    return text;
                    break;
                case 1:
                    const line = getCoffee();
                    return line;
                    break;
            }
        }
        ");

        Assert.Equal("coffee", engine.Evaluate("myFunc()"));
    }
}
