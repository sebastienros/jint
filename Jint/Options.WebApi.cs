#if NET8_0_OR_GREATER
using Jint.WebApi;

namespace Jint;

public partial class Options
{
    /// <summary>
    /// Opt-in WHATWG web platform APIs (<c>console</c>, <c>DOMException</c>, …). Nothing here is installed
    /// unless <see cref="WebApiOptions.Features"/> names it, so a default engine is byte-for-byte the engine
    /// it was before these existed.
    /// <para>
    /// <b>Requires .NET 8 or higher.</b> The whole surface is compiled only for <c>net8.0</c> and later; on
    /// <c>net462</c>, <c>netstandard2.0</c> and <c>netstandard2.1</c> the property does not exist at all.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reachable through <see cref="WebApiOptionsExtensions.UseWebApis(Options)"/> and friends, which is the
    /// spelling most hosts want. The group is here for the settings those extensions do not name.
    /// </para>
    /// <para>
    /// Unlike the other option groups this one is allocated on first touch rather than with the
    /// <see cref="Options"/> instance, so a host that never asks for a web API pays nothing for the group
    /// existing — <see cref="Apply"/> reads the backing field and never forces it. Touching the property is
    /// therefore a host-thread act like any other option mutation; an engine build never does it.
    /// </para>
    /// </remarks>
    public WebApiOptions WebApi => _webApi ??= new WebApiOptions();

    private WebApiOptions? _webApi;

    /// <summary>
    /// Configuration for the opt-in web platform APIs. Requires .NET 8 or higher.
    /// </summary>
    /// <remarks>
    /// Like every other <see cref="Options"/> group this may be shared by any number of engines, including
    /// concurrent ones: nothing on it is engine-affine. The one obligation that carries is on
    /// <see cref="ConsoleOptions.Sink"/> — see its documentation.
    /// </remarks>
    public class WebApiOptions
    {
        /// <summary>
        /// Which web APIs this engine exposes. Defaults to <see cref="WebApiFeatures.None"/>, which installs
        /// nothing at all — not even <c>DOMException</c>.
        /// </summary>
        public WebApiFeatures Features { get; set; }

        /// <summary>
        /// Settings for the <c>console</c> object, installed when <see cref="Features"/> contains
        /// <see cref="WebApiFeatures.Console"/>.
        /// </summary>
        public ConsoleOptions Console { get; } = new();
    }

    /// <summary>
    /// Settings for the <c>console</c> object. Requires .NET 8 or higher.
    /// </summary>
    public class ConsoleOptions
    {
        /// <summary>
        /// Where <c>console</c> output goes. Defaults to <see cref="ConsoleSink.Null"/>, which discards
        /// everything — enabling the feature never starts writing to the host's standard output by surprise.
        /// </summary>
        /// <remarks>
        /// The sink is read afresh on every emit, so a host may swap it between evaluations. A sink assigned
        /// to an <see cref="Options"/> instance shared by concurrently running engines is called from each of
        /// their threads and must be thread-safe; one belonging to a single engine is only ever called on
        /// that engine's thread. Assigning <see langword="null"/> is read back as
        /// <see cref="ConsoleSink.Null"/>.
        /// </remarks>
        public ConsoleSink Sink { get; set; } = ConsoleSink.Null;
    }
}

/// <summary>
/// The web platform APIs an engine can be asked to expose. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// Unlike TC39 built-ins, which Jint registers unconditionally, WHATWG web APIs are host APIs: an engine
/// embedded in a workflow runner or a template renderer has no business carrying them, so they are installed
/// only when named here.
/// </para>
/// <para>
/// The bit layout is fixed ahead of the implementations so that a value persisted by a host keeps its meaning
/// as the surface grows. The bits reserved for the features still to land are
/// <c>Timers = 1 &lt;&lt; 1</c>, <c>Encoding = 1 &lt;&lt; 2</c>, <c>Base64 = 1 &lt;&lt; 3</c>,
/// <c>StructuredClone = 1 &lt;&lt; 4</c>, <c>Crypto = 1 &lt;&lt; 5</c>, <c>Performance = 1 &lt;&lt; 6</c>,
/// <c>Events = 1 &lt;&lt; 7</c>, <c>Url = 1 &lt;&lt; 8</c>, <c>Files = 1 &lt;&lt; 9</c> and
/// <c>Fetch = 1 &lt;&lt; 10</c>. A flag is declared here only once the feature behind it actually exists, so
/// that naming one can never compile into an engine that silently does not have it.
/// </para>
/// <para>
/// <see cref="Default"/> grows as each non-network feature lands, and <b>will never include the fetch
/// flag</b>: outbound network access from script is a decision a host has to make explicitly, never one it
/// inherits from asking for "the web APIs".
/// </para>
/// </remarks>
[Flags]
public enum WebApiFeatures
{
    /// <summary>
    /// No web API is installed. This is the default, and the engine is then indistinguishable from one built
    /// by a Jint that never had this feature.
    /// </summary>
    None = 0,

    /// <summary>
    /// The <c>console</c> object (<c>log</c>, <c>warn</c>, <c>group</c>, <c>count</c>, <c>time</c>, …). Output
    /// goes to <see cref="Options.ConsoleOptions.Sink"/>, which discards it unless the host sets one.
    /// </summary>
    Console = 1 << 0,

    /// <summary>
    /// The web APIs a host normally wants: everything except outbound network access. Today that is
    /// <see cref="Console"/>; it grows as further features land, and never comes to include fetch.
    /// </summary>
    Default = Console,
}
#endif
