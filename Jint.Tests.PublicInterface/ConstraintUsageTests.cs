namespace Jint.Tests.PublicInterface;

[Collection("ConstraintUsageTests")]
public class ConstraintUsageTests
{
    [Fact]
    public void CanObserveConstraintsFromCustomCode()
    {
        var engine = new Engine(o => o.TimeoutInterval(TimeSpan.FromMilliseconds(100)));
        engine.SetValue("slowFunction", new Func<string>(() =>
        {
            for (var i = 0; i < 100; ++i)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(200));
                engine.Constraints.Check();
            }

            return "didn't throw!";
        }));

        Invoking(() => engine.Execute("slowFunction()")).Should().ThrowExactly<TimeoutException>();
    }
}
