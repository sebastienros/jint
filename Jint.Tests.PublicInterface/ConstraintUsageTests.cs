using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public class ConstraintUsageTests
{
    [Fact]
    public void CanFindAndResetCancellationConstraint()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
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
                    while (new Date() < totalMilliseconds) { }
                }

                sleep(200);
                return 'done';
            ");
            return result.AsString();
        }
    }
}
