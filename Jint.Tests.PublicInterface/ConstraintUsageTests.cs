namespace Jint.Tests.PublicInterface;

[Collection("ConstraintUsageTests")]
public class ConstraintUsageTests
{
// this test case is problematic due to nature of cancellation token source in old framework
// in NET 6 it's better designed and signals more reliably

// TODO NET 8 also has problems with this
#if NET6_0
    [Fact]
    public void CanFindAndResetCancellationConstraint()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var engine = new Engine(new Options().CancellationToken(cts.Token));

        // expect constraint to abort execution due to timeout
        Assert.Throws<ExecutionCanceledException>(WaitAndCompute);

        // ensure constraint can be obtained publicly
        var cancellationConstraint = engine.FindConstraint<CancellationConstraint>();
        Assert.NotNull(cancellationConstraint);

        // reset constraint, expect computation to finish this time
        using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        cancellationConstraint.Reset(cts2.Token);
        Assert.Equal("done", WaitAndCompute());

        string WaitAndCompute()
        {
            var result = engine.Evaluate(@"
                function sleep(millisecondsTimeout) {
                    var totalMilliseconds = new Date().getTime() + millisecondsTimeout;
                    var now = new Date().getTime();
                    while (now < totalMilliseconds) {
                        // simulate some work
                        now = new Date().getTime();
                        now = new Date().getTime();
                        now = new Date().getTime();
                        now = new Date().getTime();
                        now = new Date().getTime();
                    }
                }
                sleep(300);
                return 'done';
            ");
            return result.AsString();
        }
    }
#endif

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

        Assert.Throws<TimeoutException>(() => engine.Execute("slowFunction()"));
    }
}
