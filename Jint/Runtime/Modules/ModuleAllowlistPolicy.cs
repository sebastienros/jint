namespace Jint.Runtime.Modules;

/// <summary>
/// A reusable <see cref="IModuleLoadPolicy"/> that allowlists modules by URI scheme, host, origin
/// (scheme+host+effective port), and filesystem root path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Composition rules:</b> dimensions are combined with AND — a module must satisfy every configured
/// dimension. Within each dimension, entries are combined with OR — satisfying any one entry in a list is
/// enough. An unconfigured dimension (empty list) imposes no restriction.
/// </para>
/// <para>
/// A file target cannot satisfy a configured host or origin dimension, and a non-file target cannot satisfy a
/// configured filesystem-root dimension, so either mismatch is denied. Configure only dimensions meaningful
/// for the target kind, plus <see cref="AllowedSchemes"/> when both file and non-file targets are permitted.
/// </para>
/// <para>
/// Bare specifiers — those with no URI at all — are denied by default when any dimension is configured.
/// Set <see cref="AllowBareSpecifiers"/> to <c>true</c> to permit them. When no dimension is configured
/// (all lists empty), everything is allowed.
/// </para>
/// <para>
/// Filesystem path comparisons are case-insensitive on Windows and case-sensitive elsewhere. Both configured
/// roots and resolved files are canonicalized with <see cref="Path.GetFullPath(string)"/> and checked with
/// separator-aware boundary logic so <c>/app</c> does not match <c>/application/</c>. Relative roots never
/// match. This is lexical containment: it does not resolve symbolic links or Windows reparse points, so an
/// allowed root must not contain attacker-controlled links to files outside it.
/// </para>
/// </remarks>
public sealed class ModuleAllowlistPolicy : IModuleLoadPolicy
{
    private static readonly StringComparison FilePathComparison = Path.DirectorySeparatorChar == '\\'
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Allowed URI schemes (case-insensitive). Example: <c>"https"</c>, <c>"file"</c>.
    /// Empty means no scheme restriction.
    /// </summary>
    public List<string> AllowedSchemes { get; } = new();

    /// <summary>
    /// Allowed exact hosts (case-insensitive). Example: <c>"cdn.example.com"</c>.
    /// A configured host list denies file URIs, which cannot satisfy it. Empty means no host restriction.
    /// </summary>
    public List<string> AllowedHosts { get; } = new();

    /// <summary>
    /// Allowed origins as <c>scheme://host[:port]</c> (case-insensitive). Omitted default ports are compared by
    /// their effective value, so <c>"https://cdn.example.com"</c> matches port 443.
    /// A configured origin list denies file URIs, which cannot satisfy it. Empty means no origin restriction.
    /// </summary>
    public List<string> AllowedOrigins { get; } = new();

    /// <summary>
    /// Allowed filesystem root directories. Each entry must be an absolute path. A module file must reside under
    /// (or equal to) at least one entry. Example: <c>"/app/scripts"</c>.
    /// A configured root list denies non-file URIs, which cannot satisfy it. Empty means no file-path
    /// restriction.
    /// </summary>
    public List<string> AllowedFileRoots { get; } = new();

    /// <summary>
    /// Whether to allow bare specifiers (those that resolve with no URI). Default is <c>false</c>. When any
    /// policy dimension is configured and this is <c>false</c>, bare specifiers are denied. When no policy is
    /// active (the engine default), this property has no effect.
    /// </summary>
    public bool AllowBareSpecifiers { get; set; }

    internal bool HasAnyRestriction =>
        AllowedSchemes.Count > 0
        || AllowedHosts.Count > 0
        || AllowedOrigins.Count > 0
        || AllowedFileRoots.Count > 0;

    internal bool HasDestinationBoundary =>
        AllowedHosts.Count > 0
        || AllowedOrigins.Count > 0
        || AllowedFileRoots.Count > 0;

    /// <inheritdoc />
    public bool AllowLoad(string? referrerLocation, ModuleRequest request, ResolvedSpecifier resolved)
    {
        var uri = resolved.Uri;

        // Bare / no-URI specifiers.
        if (uri is null || !uri.IsAbsoluteUri)
        {
            return !HasAnyRestriction || AllowBareSpecifiers;
        }

        // Scheme check — applies to every absolute URI.
        if (AllowedSchemes.Count > 0)
        {
            var allowed = false;
            foreach (var scheme in AllowedSchemes)
            {
                if (string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                return false;
            }
        }

        if (uri.IsFile)
        {
            // A file URI cannot satisfy a host or origin restriction.
            if (AllowedHosts.Count > 0 || AllowedOrigins.Count > 0)
            {
                return false;
            }

            if (AllowedFileRoots.Count > 0)
            {
                var filePath = uri.LocalPath;
                var allowed = false;
                foreach (var root in AllowedFileRoots)
                {
                    if (IsUnderRoot(filePath, root))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    return false;
                }
            }
        }
        else
        {
            // A non-file URI cannot satisfy a filesystem root restriction.
            if (AllowedFileRoots.Count > 0)
            {
                return false;
            }

            if (AllowedHosts.Count > 0)
            {
                var allowed = false;
                foreach (var host in AllowedHosts)
                {
                    if (string.Equals(uri.IdnHost, host, StringComparison.OrdinalIgnoreCase))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    return false;
                }
            }

            if (AllowedOrigins.Count > 0)
            {
                var allowed = false;
                foreach (var o in AllowedOrigins)
                {
                    if (Uri.TryCreate(o, UriKind.Absolute, out var allowedOrigin)
                        && string.Equals(uri.Scheme, allowedOrigin.Scheme, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(uri.IdnHost, allowedOrigin.IdnHost, StringComparison.OrdinalIgnoreCase)
                        && uri.Port == allowedOrigin.Port)
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsUnderRoot(string filePath, string root)
    {
        if (!Path.IsPathRooted(root))
        {
            return false;
        }

        var canonicalFile = Path.GetFullPath(filePath);
        var canonicalRoot = Path.GetFullPath(root);
        if (string.Equals(canonicalFile, canonicalRoot, FilePathComparison))
        {
            return true;
        }

        if (!canonicalRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            && !canonicalRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            canonicalRoot += Path.DirectorySeparatorChar;
        }

        return canonicalFile.StartsWith(canonicalRoot, FilePathComparison);
    }
}
