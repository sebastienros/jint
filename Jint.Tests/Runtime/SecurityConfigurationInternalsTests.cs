using Jint.Constraints;

namespace Jint.Tests.Runtime;

public class SecurityConfigurationInternalsTests
{
    [Test]
    public void FirstPartyConfigurationHasUnspoofableDiagnosticProvenance()
    {
        var callbackCount = 0;
        var options = CreateSafeOptions().ConfigureFirstParty(engine =>
        {
            callbackCount++;
            engine.Options.Strict = true;
        });

        options.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();

        var engine = new Engine(options);

        callbackCount.Should().Be(1);
        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
    }

    private static Options CreateSafeOptions()
    {
        var options = new Options()
            .LimitStatements(100_000)
            .LimitMemory(4_000_000)
            .LimitExecutionTime(TimeSpan.FromSeconds(2))
            .ObserveCancellation(new CancellationTokenSource().Token)
            .AddConstraint(static () => new OperationDeadlineConstraint());
        options.Constraints.MaxRecursionDepth = 64;
        options.Constraints.MaxArraySize = 100_000;
        options.Constraints.RegexTimeout = TimeSpan.FromSeconds(1);
        options.Host.StringCompilationAllowed = false;
        options.Interop.AllowWrite = false;

        options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(1);
        options.Parsing.MaxSourceLength = 100_000;
        options.Parsing.MaxNodeCount = 25_000;
        options.ResultLimits = ResultLimits.Conservative;
        return options;
    }
}
