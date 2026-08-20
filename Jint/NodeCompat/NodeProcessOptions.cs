namespace Jint.NodeCompat;

/// <summary>
/// Configuration for the opt-in Node-style <c>process</c> global, installed by
/// <see cref="NodeProcessOptionsExtensions.UseNodeProcess(Options, Action{NodeProcessOptions}?)"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a <i>shim</i>, not an emulation: it exposes the handful of <c>process</c> members a script written
/// for Node reaches for when it is really only asking "which platform am I on" and "what is in the
/// environment", and it exposes nothing that would let a script act on the host process. There is no
/// <c>exit</c>, no <c>abort</c>, no <c>kill</c>, no <c>argv</c> content and no real working directory, and
/// <b>not one environment variable is readable until the host lists it</b> —
/// see <see cref="EnvironmentVariableAllowlist"/>.
/// </para>
/// <para>
/// Every value here is read once, when <c>UseNodeProcess</c> returns, and copied into an immutable snapshot.
/// Mutating this object afterwards — or the collections handed to it — changes nothing, which is what makes
/// one <see cref="Options"/> instance shared by concurrently constructed engines safe: no engine build ever
/// reads a collection the host may be writing to.
/// </para>
/// <para>
/// Unlike the WHATWG web APIs this compiles for every target framework: it depends on nothing newer than
/// <c>netstandard2.0</c>.
/// </para>
/// </remarks>
public sealed class NodeProcessOptions
{
    /// <summary>
    /// The value <see cref="Version"/> reports unless the host replaces it.
    /// </summary>
    internal const string JintVersionString = "v0.0.0-jint";

    private IReadOnlyCollection<string> _environmentVariableAllowlist = [];
    private string _platform = NodePlatform.Default();
    private string _version = JintVersionString;
    private string _workingDirectory = "/";

    /// <summary>
    /// The environment variable names <c>process.env</c> is allowed to expose. <b>Empty by default, which
    /// exposes nothing at all.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list is the whole gate, and it gates <see cref="EnvironmentOverrides"/> too: a name that is not
    /// here is absent from <c>process.env</c> however it might have got a value, so a test host that supplies
    /// an override cannot accidentally widen what a script can read. A name that is here but has no value —
    /// no override and no such variable in the real environment — is simply absent as well, exactly as it
    /// would be in Node.
    /// </para>
    /// <para>
    /// Only the listed names are ever read: the engine asks
    /// <see cref="Environment.GetEnvironmentVariable(string)"/> for each of them in turn and never enumerates
    /// the environment block, so a variable the host did not name is not read, not copied and not reachable
    /// from a heap dump of the engine. The lookup is the operating system's, which on Windows means it is
    /// case-insensitive; the property name a script sees is the one spelled here.
    /// </para>
    /// <para>
    /// Order is preserved and duplicates are dropped, so <c>Object.keys(process.env)</c> answers in the order
    /// the host listed. Assigning <see langword="null"/> is read back as an empty collection.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<string> EnvironmentVariableAllowlist
    {
        get => _environmentVariableAllowlist;
        set => _environmentVariableAllowlist = value ?? [];
    }

    /// <summary>
    /// Values that stand in for the real environment. <see langword="null"/> by default, meaning every
    /// allowed name is answered from the real environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry wins over the real environment outright — including an entry whose value is
    /// <see langword="null"/>, which makes the variable absent rather than falling through to the real one,
    /// so a host can hide a variable it also allowed. Entries are still filtered by
    /// <see cref="EnvironmentVariableAllowlist"/>: add the name there to expose the value.
    /// </para>
    /// <para>
    /// This is what makes a suite that exercises <c>process.env</c> deterministic without touching the
    /// machine's environment, and it is also how a host projects configuration it holds itself — an
    /// <c>IConfiguration</c> section, a tenant's settings — into a script that expects to read it from the
    /// environment.
    /// </para>
    /// <para>
    /// The dictionary is copied when <c>UseNodeProcess</c> returns, keeping its comparer when it is a
    /// <see cref="Dictionary{TKey, TValue}"/> so a case-insensitive one keeps behaving that way; anything
    /// else is looked up ordinally.
    /// </para>
    /// </remarks>
    public IDictionary<string, string>? EnvironmentOverrides { get; set; }

    /// <summary>
    /// What <c>process.platform</c> reports. Defaults to the platform this process is running on, as one of
    /// Node's own platform strings: <c>"win32"</c>, <c>"darwin"</c> or <c>"linux"</c>.
    /// </summary>
    /// <remarks>
    /// The value is the one thing here a script is likely to branch on — <c>process.platform === 'win32'</c>
    /// deciding a path separator — so it answers truthfully by default rather than claiming a platform the
    /// host is not on. Node's set also includes <c>'aix'</c>, <c>'freebsd'</c>, <c>'openbsd'</c> and
    /// <c>'sunos'</c> (https://nodejs.org/api/process.html#processplatform); Jint reports <c>"linux"</c> on
    /// any of them, being the closest POSIX answer, and a host that needs one of those exact strings sets it
    /// here. Assigning <see langword="null"/> is read back as the detected default.
    /// </remarks>
    public string Platform
    {
        get => _platform;
        set => _platform = value ?? NodePlatform.Default();
    }

    /// <summary>
    /// What <c>process.version</c> reports. Defaults to <c>"v0.0.0-jint"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The default deliberately is not a Node version.</b> Node's <c>process.version</c> is the version of
    /// the runtime, and a script that reads it is asking what it may rely on; answering <c>"v22.11.0"</c>
    /// would be a claim to be a Node release that Jint does not implement, and a library gating a native
    /// module, a syntax feature or a built-in on that number would take the wrong branch and fail somewhere
    /// far away from the lie. A version below every Node release, carrying the runtime's real name, is the
    /// honest answer to "which Node is this": none of them.
    /// </para>
    /// <para>
    /// It is settable because the other half of that story is real too: a dependency that flatly refuses to
    /// load below some version, or a compatibility shim whose only use of <c>process.version</c> is a
    /// <c>semver</c> comparison, needs a number to get past. Setting it is the host saying "I have checked
    /// what this script does with it", which is a decision Jint should not make silently. The value is not
    /// validated — Node's own format is <c>v</c> followed by a semantic version.
    /// </para>
    /// <para>
    /// <c>process.versions</c> is unaffected and always reports Jint's own assembly version under the
    /// <c>jint</c> key, and no <c>node</c> key at all, so feature detection has something truthful to find
    /// whatever this is set to. Assigning <see langword="null"/> is read back as the default.
    /// </para>
    /// </remarks>
    public string Version
    {
        get => _version;
        set => _version = value ?? JintVersionString;
    }

    /// <summary>
    /// What <c>process.cwd()</c> returns. Defaults to <c>"/"</c>.
    /// </summary>
    /// <remarks>
    /// <b>The real current directory is never used.</b> <see cref="Environment.CurrentDirectory"/> of a server
    /// process names a deployment layout, often a user account and sometimes a customer, none of which a
    /// script has any business learning; and a script that resolved paths against it would be reaching for
    /// files by an absolute path that means something to the host and nothing to it. So <c>cwd()</c> answers
    /// this configured value and only this configured value: a host that genuinely wants the script to see a
    /// directory names one here — the sandbox root it hands the script, typically the same directory its
    /// module loader is rooted at. Assigning <see langword="null"/> is read back as <c>"/"</c>.
    /// </remarks>
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => _workingDirectory = value ?? "/";
    }
}
