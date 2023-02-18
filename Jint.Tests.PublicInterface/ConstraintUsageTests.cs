using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

[Collection("ConstraintUsageTests")]
public class ConstraintUsageTests
{
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
}
