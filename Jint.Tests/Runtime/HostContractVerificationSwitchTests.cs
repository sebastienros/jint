#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the harness half of the host-contract verification story: that
/// <see cref="HostContractVerificationSwitch"/> — which every verification-aware expectation in both test suites
/// selects on — reports the same thing Jint's own gate does.
///
/// <para>
/// The two are computed independently and from different inputs. Jint reads the AppContext switch
/// <c>Jint.EnableHostContractVerification</c> once, at the type initialization of
/// <c>HostContractVerification</c>; the harness reads the environment variable
/// <c>JINT_HOST_CONTRACT_VERIFICATION</c> and sets that switch from a module initializer. They can only agree if
/// the module initializer really did run before the first use of any Jint type — which is the entire ordering
/// contract of a <c>static readonly</c> gate, and the thing a fixture or a class constructor would be too late
/// for. This project has <c>InternalsVisibleTo</c>, so it can read the gate directly; the same claim is made
/// from behaviour alone in <c>Jint.Tests.PublicInterface.HostContractVerificationTests</c>.
/// </para>
/// </summary>
public class HostContractVerificationSwitchTests
{
    [Fact]
    public void TheHarnessSwitchAgreesWithJintsOwnGate()
    {
        HostContractVerificationSwitch.Enabled.Should().Be(
            HostContractVerification.Enabled,
            "the module initializer must set the AppContext switch before Jint's gate reads it");
    }

    [Fact]
    public void AskingForVerificationIsWhatTurnsItOnOutsideDebug()
    {
#if DEBUG
        HostContractVerification.Enabled.Should().BeTrue("a Debug build verifies host contracts without being asked");
#else
        HostContractVerification.Enabled.Should().Be(
            HostContractVerificationSwitch.RequestedByTheEnvironment(),
            "a Release run verifies exactly when " + HostContractVerificationSwitch.EnvironmentVariableName + " asked it to");
#endif
    }
}
