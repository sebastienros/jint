using Jint.Constraints;

namespace Jint.Tests.Runtime;

public class SecurityConfigurationInternalsTests
{
    [Fact]
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
        engine.Advanced.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
    }

    private static Options CreateSafeOptions()
    {
        var options = new Options()
            .MaxStatements(100_000)
            .LimitMemory(4_000_000)
            .TimeoutInterval(TimeSpan.FromSeconds(2))
            .CancellationToken(new CancellationTokenSource().Token)
            .Constraint(static () => new OperationDeadlineConstraint())
            .LimitRecursion(64)
            .MaxArraySize(100_000)
            .RegexTimeoutInterval(TimeSpan.FromSeconds(1))
            .DisableStringCompilation()
            .AllowClrWrite(false);

        options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(1);
        options.Parsing.MaxSourceLength = 100_000;
        options.Parsing.MaxNodeCount = 25_000;
        options.ResultLimits = ResultLimits.Conservative;
        return options;
    }
}
