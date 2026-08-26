namespace Jint.Tests.PublicInterface;

public class ConstraintUsageTests
{
    [Test]
    public void CanObserveConstraintsFromCustomCode()
    {
        var engine = new Engine(o => o.LimitExecutionTime(TimeSpan.FromMilliseconds(100)));
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
