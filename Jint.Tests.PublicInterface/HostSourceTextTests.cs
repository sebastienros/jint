#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Engine.Advanced.TryGetSourceText</c> from the outside: the source a program was parsed from, keyed by
/// the <see cref="Program"/> node <c>DebugHandler.BeforeEvaluate</c> hands a host.
/// </summary>
/// <remarks>
/// The motivating consumer is a tooling protocol — Chrome DevTools' <c>Debugger.getScriptSource</c> answers
/// with exactly this — but the engine gives a host no other way back to the text: the AST carries locations
/// into a string nobody kept. It rides on <c>Options.RetainFunctionSourceText</c>, which is what
/// <c>Function.prototype.toString</c> already retains the same string for.
/// </remarks>
public class HostSourceTextTests
{
    /// <summary>
    /// A source string that cannot be reference-equal to anything by interning, so that
    /// <see cref="object.ReferenceEquals"/> below means "the very string handed in" rather than "a string
    /// that happens to read the same".
    /// </summary>
    private static string UniqueScript()
        => string.Concat("function greet() { return 'hi'; } greet();", " // ", Guid.NewGuid().ToString());

    private static string UniqueModule()
        => string.Concat("export function greet() { return 'hi'; }", " // ", Guid.NewGuid().ToString());

    private static Program CaptureExecuted(Engine engine, string code)
    {
        Program? captured = null;
        engine.Debugger.BeforeEvaluate += (_, ast) => captured ??= ast;
        engine.Execute(code);
        captured.Should().NotBeNull();
        return captured!;
    }

    /// <summary>
    /// The round trip a host asks for: the program the debugger announced answers with the exact string that
    /// was executed, and the same switch is the one that makes <c>toString()</c> answer.
    /// </summary>
    [Test]
    public void AScriptAnswersWithTheStringItWasExecutedFrom()
    {
        var code = UniqueScript();

        using var engine = new Engine(options => options.RetainFunctionSourceText = true);

        var program = CaptureExecuted(engine, code);

        engine.Advanced.TryGetSourceText(program, out var text).Should().BeTrue();
        ReferenceEquals(text, code).Should().BeTrue();

        engine.Evaluate("greet.toString()").AsString().Should().Be("function greet() { return 'hi'; }");
    }

    /// <summary>
    /// A module is announced through the same event and answers the same way, which is what makes the two
    /// interchangeable to a caller holding nothing but a <see cref="Program"/>.
    /// </summary>
    [Test]
    public void AModuleAnswersWithTheStringItWasBuiltFrom()
    {
        var code = UniqueModule();

        using var engine = new Engine(options => options.RetainFunctionSourceText = true);

        Program? captured = null;
        engine.Debugger.BeforeEvaluate += (_, ast) => captured ??= ast;

        engine.Modules.Add("my-module", code);
        engine.Modules.Import("my-module");

        captured.Should().NotBeNull();
        engine.Advanced.TryGetSourceText(captured!, out var text).Should().BeTrue();
        ReferenceEquals(text, code).Should().BeTrue();
    }

    /// <summary>
    /// With retention off — the default — nothing is kept, so the answer is <see langword="false"/> rather
    /// than a reconstruction. One switch decides both this and <c>Function.prototype.toString</c>.
    /// </summary>
    [Test]
    public void NothingIsKeptWhenTheParseDidNotRetainSourceText()
    {
        var code = UniqueScript();

        using var engine = new Engine();

        var program = CaptureExecuted(engine, code);

        engine.Advanced.TryGetSourceText(program, out var text).Should().BeFalse();
        text.Should().BeNull();
    }

    /// <summary>
    /// A <see cref="Prepared{TProgram}"/> is parsed once, with no engine in sight, and shared: every engine
    /// running it answers with the same string, including one that never ran it at all.
    /// </summary>
    [Test]
    public void APreparedScriptAnswersTheSameTextOnEveryEngine()
    {
        var code = UniqueScript();

        var prepared = Engine.PrepareScript(code, options: new ScriptPreparationOptions
        {
            ParsingOptions = new ScriptParsingOptions { RetainFunctionSourceText = true },
        });

        using var first = new Engine();
        using var second = new Engine();
        using var neverRanIt = new Engine();

        first.Execute(prepared);
        second.Execute(prepared);

        first.Advanced.TryGetSourceText(prepared.Program!, out var fromFirst).Should().BeTrue();
        second.Advanced.TryGetSourceText(prepared.Program!, out var fromSecond).Should().BeTrue();
        neverRanIt.Advanced.TryGetSourceText(prepared.Program!, out var fromStranger).Should().BeTrue();

        ReferenceEquals(fromFirst, code).Should().BeTrue();
        ReferenceEquals(fromSecond, code).Should().BeTrue();
        ReferenceEquals(fromStranger, code).Should().BeTrue();
    }

    /// <summary>
    /// The preparation's own switch is what decides, not the engine's: a script prepared without retention
    /// answers <see langword="false"/> on an engine configured to retain.
    /// </summary>
    [Test]
    public void APreparedScriptFollowsItsOwnRetentionSetting()
    {
        var code = UniqueScript();

        var prepared = Engine.PrepareScript(code);

        using var engine = new Engine(options => options.RetainFunctionSourceText = true);
        engine.Execute(prepared);

        engine.Advanced.TryGetSourceText(prepared.Program!, out var text).Should().BeFalse();
        text.Should().BeNull();
    }

    /// <summary>
    /// Preparation has two parse shapes — with the static analysis pass and without it — and the retention
    /// setting means the same thing in both.
    /// </summary>
    [Test]
    public void AParseOnlyPreparationRetainsTheSameText()
    {
        var code = UniqueScript();

        var prepared = Engine.PrepareScript(code, options: new ScriptPreparationOptions
        {
            StaticAnalysis = false,
            CollectReferencedGlobals = true,
            ParsingOptions = new ScriptParsingOptions { RetainFunctionSourceText = true },
        });

        using var engine = new Engine();
        engine.Execute(prepared);

        engine.Advanced.TryGetSourceText(prepared.Program!, out var text).Should().BeTrue();
        ReferenceEquals(text, code).Should().BeTrue();
    }

    /// <summary>
    /// A prepared module is the module half of the same promise.
    /// </summary>
    [Test]
    public void APreparedModuleAnswersWithItsOwnText()
    {
        var code = UniqueModule();

        var prepared = Engine.PrepareModule(code, options: new ModulePreparationOptions
        {
            ParsingOptions = new ModuleParsingOptions { RetainFunctionSourceText = true },
        });

        using var engine = new Engine();
        engine.Modules.Add("my-module", builder => builder.AddModule(prepared));
        engine.Modules.Import("my-module");

        engine.Advanced.TryGetSourceText(prepared.Program!, out var text).Should().BeTrue();
        ReferenceEquals(text, code).Should().BeTrue();
    }

    [Test]
    public void TryGetSourceTextRefusesANullProgram()
    {
        using var engine = new Engine();

        Invoking(() => engine.Advanced.TryGetSourceText(null!, out _)).Should().Throw<ArgumentNullException>();
    }
}
