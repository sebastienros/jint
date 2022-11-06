namespace Jint.Tests.Runtime;

public class ClassTests
{
    [Fact]
    public void IsBlockScoped()
    {
        const string Script = @"
            class C {}
            var c1 = C;
            {
              class C {}
              var c2 = C;
            }
            return C === c1;";

        var engine = new Engine();
        Assert.True(engine.Evaluate(Script).AsBoolean());
    }
}
