using System.Runtime.CompilerServices;
using Jint.NodeCompat;
using Jint.Runtime;

namespace Jint;

public sealed partial class Options
{
    /// <summary>
    /// The <c>node:</c> builtin modules an engine built from these options provides, or null when the host
    /// never asked for them — which is the default, and the state in which nothing about module resolution
    /// changes at all.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="NodeBuiltinModuleOptionsExtensions.UseNodeBuiltinModules"/> and read by
    /// <c>Options.Apply</c>, which is where the configured module loader is wrapped. Deliberately not a
    /// mutation of <see cref="Modules"/>'s loader: the host's <see cref="ModuleOptions.ModuleLoader"/> keeps
    /// reading back exactly what the host set — the same posture <c>WebApiFeatures</c> takes about the feature
    /// closure fetch brings with it — and the order of <c>UseNodeBuiltinModules</c> and
    /// <c>UseModules</c> stops mattering.
    /// </remarks>
    internal NodeBuiltinModuleConfiguration? _nodeBuiltinModules;

    /// <summary>
    /// The one door onto <see cref="_nodeBuiltinModules"/>, so that <c>UseNodeBuiltinModules</c> refuses after
    /// the freeze exactly as an assignment does.
    /// </summary>
    /// <param name="configuration">The snapshot of what the host configured.</param>
    /// <param name="verb">
    /// Filled in by the compiler with the extension method the host actually called, so that the refusal names
    /// that rather than this private door.
    /// </param>
    internal void SetNodeBuiltinModules(NodeBuiltinModuleConfiguration configuration, [CallerMemberName] string? verb = null)
    {
        if (_readOnly)
        {
            Throw.OptionsReadOnlyCall("Options." + verb);
        }

        _nodeBuiltinModules = configuration;
    }
}
