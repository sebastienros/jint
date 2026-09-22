using AngleSharp;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>Bounds repeated enumeration while constructing one document's service configuration.</summary>
internal static class PageConfiguration
{
    /// <summary>
    /// Keeps service order, instances and creator delegates, without resolving any service. AngleSharp's
    /// replacement extensions compose deferred set differences that repeatedly enumerate their inputs;
    /// taking a snapshot between stages prevents later replacements and lookups from replaying the chain.
    /// The snapshot is per parse: mutable factories and page-affine services must never be cached globally.
    /// </summary>
    internal static IConfiguration MaterializeServices(this IConfiguration configuration)
        => new Configuration(configuration.Services.ToArray());
}
