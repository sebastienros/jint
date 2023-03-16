namespace Jint.Tests.PublicInterface;

public class AdvancedApiTests
{
    [Fact]
    public void CanProcessTasks()
    {
        var engine = new Engine();
        engine.Advanced.ProcessTasks();
    }
}
