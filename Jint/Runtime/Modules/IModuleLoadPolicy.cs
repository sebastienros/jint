namespace Jint.Runtime.Modules;

/// <summary>
/// A host-supplied policy that can deny a module load based on the resolved specifier and referrer context.
/// Implement this interface and assign it to <see cref="Options.ModuleOptions.LoadPolicy"/> to restrict which
/// modules may be loaded.
/// </summary>
/// <remarks>
/// <para>
/// Policy is consulted after the module loader has resolved the specifier but before the module source is
/// fetched or built. A denial throws <see cref="ModuleResolutionException"/> and follows existing
/// sync/rejection behavior — it does not propagate like a constraint exception.
/// </para>
/// <para>
/// The <see cref="DefaultModuleLoader"/>'s own base-path restriction runs earlier and independently; a policy
/// registered here applies to the final <see cref="ResolvedSpecifier"/>, regardless of loader.
/// </para>
/// </remarks>
public interface IModuleLoadPolicy
{
    /// <summary>
    /// Returns <c>true</c> if the module identified by <paramref name="resolved"/> may be loaded.
    /// </summary>
    /// <param name="referrerLocation">The <see cref="ModuleRecord.Location"/> of the importing module, or
    /// <c>null</c> for a root/host import.</param>
    /// <param name="request">The import request as written in source.</param>
    /// <param name="resolved">The resolved specifier the module loader produced.</param>
    /// <returns><c>true</c> to allow; <c>false</c> to deny.</returns>
    bool AllowLoad(string? referrerLocation, ModuleRequest request, ResolvedSpecifier resolved);
}
