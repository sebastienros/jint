using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Acornima.Ast;
using Jint.Runtime.Debugger;

namespace Jint.DevTools.Domains;

/// <summary>
/// Every program one engine has parsed, under the identifier a client addresses it by.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed on the <see cref="Program"/> itself, by reference.</b> That is what makes a cached
/// <c>Prepared&lt;Script&gt;</c> run a thousand times announce one script rather than a thousand: the engine
/// raises <c>DebugHandler.BeforeEvaluate</c> per execution, and the second execution of a program already
/// registered answers the identifier the first one minted.
/// </para>
/// <para>
/// <b>It is the target's, not a session's.</b> Scripts belong to the engine, so two attachments name the same
/// program by the same identifier and a client that attaches after a run is replayed everything already
/// parsed — which is what the front end's Sources panel is built from and why the subscription is taken when
/// the target is made rather than when a client enables the domain.
/// </para>
/// <para>
/// <b>It is bounded, and that is a memory decision.</b> A registry that never forgets holds every abstract
/// syntax tree an engine ever parsed, and a host evaluating fresh strings in a loop would grow it without
/// end; the oldest entries are dropped past <see cref="MaxScripts"/>. A dropped script's source can no longer
/// be fetched and its identifier no longer resolves, and a program that is dropped and then run again is
/// announced under a new identifier. Chrome bounds its own store the same way, through
/// <c>Debugger.enable</c>'s <c>maxScriptsCacheSize</c>.
/// </para>
/// </remarks>
internal sealed class ScriptRegistry
{
    /// <summary>How many programs are remembered before the oldest is forgotten.</summary>
    internal const int MaxScripts = 1000;

    private readonly Engine _engine;
    private readonly string _prefix;
    private readonly object _gate = new();
    private readonly List<RegisteredScript> _scripts = [];
    private readonly Dictionary<Program, RegisteredScript> _byProgram = new(ProgramIdentity.Instance);
    private readonly Dictionary<string, RegisteredScript> _byId = new(StringComparer.Ordinal);
    private readonly DebugHandler.BeforeEvaluateEventHandler _beforeEvaluate;

    private int _next;
    private int _subscribed;

    /// <summary>Creates the registry of the target numbered <paramref name="targetSerial"/>.</summary>
    internal ScriptRegistry(Engine engine, int targetSerial)
    {
        _engine = engine;
        _prefix = targetSerial.ToString(CultureInfo.InvariantCulture) + ".";
        _beforeEvaluate = OnBeforeEvaluate;
    }

    /// <summary>Raised on the engine thread for each program the engine parses, once per program.</summary>
    internal event Action<RegisteredScript>? Parsed;

    /// <summary>Starts listening to the engine, which a target does once and only when it may.</summary>
    internal void Start()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) != 0)
        {
            return;
        }

        _engine.Debugger.BeforeEvaluate += _beforeEvaluate;
    }

    /// <summary>Stops listening and forgets everything, which is what disposing the target means.</summary>
    internal void Stop()
    {
        if (Interlocked.Exchange(ref _subscribed, 0) == 0)
        {
            return;
        }

        _engine.Debugger.BeforeEvaluate -= _beforeEvaluate;

        lock (_gate)
        {
            _scripts.Clear();
            _byProgram.Clear();
            _byId.Clear();
        }
    }

    /// <summary>Answers every script still remembered, oldest first.</summary>
    internal RegisteredScript[] Snapshot()
    {
        lock (_gate)
        {
            return [.. _scripts];
        }
    }

    /// <summary>Answers the script <paramref name="scriptId"/> names, or <see langword="null"/>.</summary>
    internal RegisteredScript? ById(string scriptId)
    {
        lock (_gate)
        {
            return _byId.GetValueOrDefault(scriptId);
        }
    }

    /// <summary>
    /// Answers which script a running location belongs to, or <see langword="null"/> when none is known.
    /// </summary>
    /// <remarks>
    /// A call frame carries a <c>SourceLocation</c> and not the program it came from, so the answer is
    /// reconstructed: among the scripts parsed under that source name, the one whose own range contains the
    /// position, and failing that the most recently parsed. Several unnamed scripts therefore share one
    /// source name and the newest wins, which is a divergence from Chrome — where every frame carries the
    /// script identifier the engine recorded for it.
    /// </remarks>
    internal RegisteredScript? At(string? sourceFile, int line, int column)
    {
        var url = sourceFile ?? "";

        lock (_gate)
        {
            RegisteredScript? fallback = null;
            for (var i = _scripts.Count - 1; i >= 0; i--)
            {
                var script = _scripts[i];
                if (!string.Equals(script.Url, url, StringComparison.Ordinal))
                {
                    continue;
                }

                fallback ??= script;
                if (script.Contains(line, column))
                {
                    return script;
                }
            }

            return fallback;
        }
    }

    /// <summary>Answers the source text of <paramref name="script"/>, when the parse retained it.</summary>
    internal bool TryGetSourceText(RegisteredScript script, out string? sourceText)
        => _engine.Advanced.TryGetSourceText(script.Program, out sourceText);

    private void OnBeforeEvaluate(object sender, Program ast)
    {
        RegisteredScript registered;

        lock (_gate)
        {
            if (_byProgram.ContainsKey(ast))
            {
                return;
            }

            registered = Describe(ast);
            _byProgram.Add(ast, registered);
            _byId.Add(registered.ScriptId, registered);
            _scripts.Add(registered);

            if (_scripts.Count > MaxScripts)
            {
                var evicted = _scripts[0];
                _scripts.RemoveAt(0);
                _byProgram.Remove(evicted.Program);
                _byId.Remove(evicted.ScriptId);
            }
        }

        Parsed?.Invoke(registered);
    }

    private RegisteredScript Describe(Program ast)
    {
        var scriptId = _prefix + (++_next).ToString(CultureInfo.InvariantCulture);
        var location = ast.Location;
        var url = location.SourceFile ?? "";
        var hasText = _engine.Advanced.TryGetSourceText(ast, out var sourceText);

        // The engine counts lines from one and the protocol counts them from zero; columns are 0-based in
        // both. A location the parser never filled in reads as the whole of line one.
        return new RegisteredScript(
            ast,
            scriptId,
            url,
            startLine: Math.Max(0, location.Start.Line - 1),
            startColumn: location.Start.Column,
            endLine: Math.Max(0, location.End.Line - 1),
            endColumn: location.End.Column,
            hash: Hash(hasText ? sourceText : null, url, scriptId),
            isModule: ast is Module,
            length: Math.Max(0, ast.Range.End - ast.Range.Start));
    }

    /// <summary>
    /// The digest a client compares two versions of a script by, and the one <c>setBreakpointByUrl</c>
    /// matches its <c>scriptHash</c> against.
    /// </summary>
    /// <remarks>
    /// Chrome's own hash is a bespoke non-cryptographic one and no client parses it, so what matters is that
    /// two identical sources hash alike and nothing else does. A script whose text was not retained has no
    /// content to hash, and hashing its name and identifier keeps that property rather than making every
    /// text-free script look like the same script.
    /// </remarks>
    private static string Hash(string? sourceText, string url, string scriptId)
    {
        var text = sourceText ?? (url + " " + scriptId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}

/// <summary>
/// Reference identity for a parsed program, which is what "the same script" means here.
/// </summary>
/// <remarks>
/// Spelt out rather than left to the default comparer: two abstract syntax trees with identical contents are
/// two scripts, and a day when <c>Program</c> gains value equality must not silently turn them into one.
/// </remarks>
internal sealed class ProgramIdentity : IEqualityComparer<Program>
{
    /// <summary>The one instance, since the comparer holds nothing.</summary>
    internal static readonly ProgramIdentity Instance = new();

    private ProgramIdentity()
    {
    }

    /// <inheritdoc/>
    public bool Equals(Program? x, Program? y) => ReferenceEquals(x, y);

    /// <inheritdoc/>
    public int GetHashCode(Program obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

/// <summary>
/// One program the engine parsed, as the protocol describes it.
/// </summary>
/// <remarks>
/// Every line here is already in the protocol's counting — zero-based — so that nothing downstream has to
/// remember which side of the boundary it is on.
/// </remarks>
internal sealed class RegisteredScript
{
    internal RegisteredScript(
        Program program,
        string scriptId,
        string url,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        string hash,
        bool isModule,
        int length)
    {
        Program = program;
        ScriptId = scriptId;
        Url = url;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
        Hash = hash;
        IsModule = isModule;
        Length = length;
    }

    /// <summary>Gets the abstract syntax tree, which is what the step-location walk reads.</summary>
    internal Program Program { get; }

    /// <summary>Gets the identifier a client addresses the script by.</summary>
    internal string ScriptId { get; }

    /// <summary>Gets the source name the program was parsed under, or the empty string.</summary>
    internal string Url { get; }

    /// <summary>Gets the 0-based line the program starts on.</summary>
    internal int StartLine { get; }

    /// <summary>Gets the 0-based column the program starts at.</summary>
    internal int StartColumn { get; }

    /// <summary>Gets the 0-based line the program ends on.</summary>
    internal int EndLine { get; }

    /// <summary>Gets the 0-based column the program ends at.</summary>
    internal int EndColumn { get; }

    /// <summary>Gets the digest of the program's source.</summary>
    internal string Hash { get; }

    /// <summary>Gets whether the program is a module rather than a script.</summary>
    internal bool IsModule { get; }

    /// <summary>Gets how many characters of source the program spans.</summary>
    internal int Length { get; }

    /// <summary>Whether a 1-based line and 0-based column fall inside the program's own range.</summary>
    internal bool Contains(int line, int column)
    {
        var zeroBased = line - 1;
        if (zeroBased < StartLine || zeroBased > EndLine)
        {
            return false;
        }

        if (zeroBased == StartLine && column < StartColumn)
        {
            return false;
        }

        return zeroBased != EndLine || column <= EndColumn;
    }
}
