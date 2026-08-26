#nullable enable

using Jint.NodeCompat;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The opt-in Node <c>process</c> shim seen from outside the assembly: what a host has to write to get it,
/// what a script can and cannot learn about the host through it, and the promises the host can build on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party. The pins
/// that matter most are <see cref="ADefaultEngineHasNoProcess"/> and
/// <see cref="NotOneEnvironmentVariableIsExposedUntilTheHostListsIt"/>: the shim is opt-in, and opting in
/// still exposes nothing of the machine until the host names it.
/// </remarks>
public class NodeProcessTests
{
    [Test]
    public void ADefaultEngineHasNoProcess()
    {
        var engine = new Engine();

        engine.Evaluate("typeof process").AsString().Should().Be("undefined");
        engine.Evaluate("'process' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void UseNodeProcessNeedsNoConfigurationAtAll()
    {
        var engine = new Engine(options => options.UseNodeProcess());

        engine.Evaluate("typeof process").AsString().Should().Be("object");
        engine.Evaluate("typeof process.env").AsString().Should().Be("object");
        engine.Evaluate("Object.keys(process.env).length").AsNumber().Should().Be(0);
    }

    [Test]
    public void NotOneEnvironmentVariableIsExposedUntilTheHostListsIt()
    {
        const string Name = "JINT_TEST_PROCESS_ENV_UNLISTED";
        Environment.SetEnvironmentVariable(Name, "secret");
        try
        {
            var listed = new Engine(options => options.UseNodeProcess(p => p.EnvironmentVariableAllowlist = [Name]));
            var unlisted = new Engine(options => options.UseNodeProcess(p => p.EnvironmentVariableAllowlist = ["SOMETHING_ELSE"]));

            listed.Evaluate($"process.env.{Name}").AsString().Should().Be("secret");

            // The real variable exists and the engine simply never asks for it.
            unlisted.Evaluate($"typeof process.env.{Name}").AsString().Should().Be("undefined");
            unlisted.Evaluate("Object.keys(process.env).length").AsNumber().Should().Be(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Test]
    public void AnOverrideWinsOverTheRealEnvironment()
    {
        const string Name = "JINT_TEST_PROCESS_ENV_OVERRIDDEN";
        Environment.SetEnvironmentVariable(Name, "from the machine");
        try
        {
            var engine = new Engine(options => options.UseNodeProcess(p =>
            {
                p.EnvironmentVariableAllowlist = [Name];
                p.EnvironmentOverrides = new Dictionary<string, string> { [Name] = "from the host" };
            }));

            engine.Evaluate($"process.env.{Name}").AsString().Should().Be("from the host");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Test]
    public void AnOverrideThatIsNotAllowedIsStillNotExposed()
    {
        var engine = new Engine(options => options.UseNodeProcess(p =>
        {
            p.EnvironmentVariableAllowlist = ["ALLOWED"];
            p.EnvironmentOverrides = new Dictionary<string, string> { ["ALLOWED"] = "yes", ["SNEAKY"] = "no" };
        }));

        // The allowlist gates the overrides too, so a test host supplying values cannot widen what a script
        // can read.
        engine.Evaluate("Object.keys(process.env).join(',')").AsString().Should().Be("ALLOWED");
        engine.Evaluate("typeof process.env.SNEAKY").AsString().Should().Be("undefined");
    }

    [Test]
    public void ANullOverrideHidesTheVariableRatherThanFallingBackToTheRealOne()
    {
        const string Name = "JINT_TEST_PROCESS_ENV_HIDDEN";
        Environment.SetEnvironmentVariable(Name, "from the machine");
        try
        {
            var engine = new Engine(options => options.UseNodeProcess(p =>
            {
                p.EnvironmentVariableAllowlist = [Name];
                p.EnvironmentOverrides = new Dictionary<string, string> { [Name] = null! };
            }));

            engine.Evaluate($"typeof process.env.{Name}").AsString().Should().Be("undefined");
            engine.Evaluate("Object.keys(process.env).length").AsNumber().Should().Be(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Test]
    public void AnOverrideKeepsItsDictionarysComparer()
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["node_env"] = "production" };
        var engine = new Engine(options => options.UseNodeProcess(p =>
        {
            p.EnvironmentVariableAllowlist = ["NODE_ENV"];
            p.EnvironmentOverrides = overrides;
        }));

        engine.Evaluate("process.env.NODE_ENV").AsString().Should().Be("production");
    }

    [Test]
    public void WritesToEnvNeverReachTheRealEnvironment()
    {
        const string Name = "JINT_TEST_PROCESS_ENV_WRITE";
        var engine = new Engine(options => options.UseNodeProcess(p =>
        {
            p.EnvironmentVariableAllowlist = [Name];
            p.EnvironmentOverrides = new Dictionary<string, string> { [Name] = "before" };
        }));

        engine.Execute($"process.env.{Name} = 'after'; process.env.INVENTED = 'x'; delete process.env.NOTHING;");

        // The script mutated its own copy.
        engine.Evaluate($"process.env.{Name}").AsString().Should().Be("after");
        engine.Evaluate("process.env.INVENTED").AsString().Should().Be("x");
        Environment.GetEnvironmentVariable(Name).Should().BeNull();
        Environment.GetEnvironmentVariable("INVENTED").Should().BeNull();
    }

    [Test]
    public void DeletingFromEnvIsScriptLocalToo()
    {
        const string Name = "JINT_TEST_PROCESS_ENV_DELETE";
        Environment.SetEnvironmentVariable(Name, "still here");
        try
        {
            var engine = new Engine(options => options.UseNodeProcess(p => p.EnvironmentVariableAllowlist = [Name]));

            engine.Execute($"delete process.env.{Name};");

            engine.Evaluate($"typeof process.env.{Name}").AsString().Should().Be("undefined");
            Environment.GetEnvironmentVariable(Name).Should().Be("still here");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Test]
    public void TwoEnginesFromOneOptionsGetTheirOwnEnvObject()
    {
        var options = new Options().UseNodeProcess(p =>
        {
            p.EnvironmentVariableAllowlist = ["A"];
            p.EnvironmentOverrides = new Dictionary<string, string> { ["A"] = "shared" };
        });

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("process.env.A = 'mine';");

        first.Evaluate("process.env.A").AsString().Should().Be("mine");
        second.Evaluate("process.env.A").AsString().Should().Be("shared");
    }

    [Test]
    public void TheConfigurationIsSnapshottedWhenUseNodeProcessReturns()
    {
        var overrides = new Dictionary<string, string> { ["A"] = "captured" };
        NodeProcessOptions? captured = null;

        var options = new Options().UseNodeProcess(p =>
        {
            p.EnvironmentVariableAllowlist = ["A", "B"];
            p.EnvironmentOverrides = overrides;
            captured = p;
        });

        // Whatever the host does to its own objects afterwards, the engines built from these options answer
        // from the values that were there when the call returned.
        overrides["A"] = "changed";
        overrides["B"] = "added";
        captured!.Platform = "changed";

        var engine = new Engine(options);
        engine.Evaluate("process.env.A").AsString().Should().Be("captured");
        engine.Evaluate("typeof process.env.B").AsString().Should().Be("undefined");
        engine.Evaluate("process.platform").AsString().Should().NotBe("changed");
    }

    [Test]
    public void PlatformIsTheRunningPlatformUnlessTheHostSaysOtherwise()
    {
        var engine = new Engine(options => options.UseNodeProcess());

        engine.Evaluate("process.platform").AsString().Should().BeOneOf("win32", "darwin", "linux");

        var pretending = new Engine(options => options.UseNodeProcess(p => p.Platform = "freebsd"));
        pretending.Evaluate("process.platform").AsString().Should().Be("freebsd");
    }

    [Test]
    public void VersionIsNotANodeVersionUnlessTheHostMakesItOne()
    {
        var engine = new Engine(options => options.UseNodeProcess());

        // Feature-detection honesty: below every Node release and carrying the runtime's real name, so a
        // library gating on a Node version cannot be misled into a branch Jint does not implement.
        engine.Evaluate("process.version").AsString().Should().Be("v0.0.0-jint");
        engine.Evaluate("typeof process.versions.node").AsString().Should().Be("undefined");
        engine.Evaluate("process.versions.jint").AsString().Should().Be(
            typeof(Engine).Assembly.GetName().Version!.ToString(3));

        // ... and settable, for the dependency that flatly refuses to load below some version.
        var pretending = new Engine(options => options.UseNodeProcess(p => p.Version = "v20.11.0"));
        pretending.Evaluate("process.version").AsString().Should().Be("v20.11.0");

        // Even then `versions` stays truthful.
        pretending.Evaluate("typeof process.versions.node").AsString().Should().Be("undefined");
        pretending.Evaluate("typeof process.versions.jint").AsString().Should().Be("string");
    }

    [Test]
    public void CwdAnswersTheConfiguredDirectoryAndNeverTheRealOne()
    {
        var engine = new Engine(options => options.UseNodeProcess());

        engine.Evaluate("process.cwd()").AsString().Should().Be("/");
        engine.Evaluate("process.cwd()").AsString().Should().NotBe(Environment.CurrentDirectory);

        var rooted = new Engine(options => options.UseNodeProcess(p => p.WorkingDirectory = "/app"));
        rooted.Evaluate("process.cwd()").AsString().Should().Be("/app");
    }

    [Test]
    public void NothingCanActOnTheHostProcess()
    {
        var engine = new Engine(options => options.UseNodeProcess());

        foreach (var name in new[] { "exit", "abort", "kill", "chdir" })
        {
            engine.Evaluate($"typeof process.{name}").AsString().Should().Be("undefined");
        }
    }

    [Test]
    public void NextTickQueuesOntoTheEnginesOwnJobQueue()
    {
        var engine = new Engine(options => options.UseNodeProcess());

        engine.Execute("""
            var log = [];
            process.nextTick((a, b) => log.push('tick(' + a + ',' + b + ')'), 1, 2);
            log.push('script');
            """);

        engine.Evaluate("log.join(',')").AsString().Should().Be("script,tick(1,2)");
    }

    [Test]
    public void TheNpmStyleProbeAScriptActuallyWritesWorks()
    {
        var engine = new Engine(options => options.UseNodeProcess(p =>
        {
            p.EnvironmentVariableAllowlist = ["NODE_ENV"];
            p.EnvironmentOverrides = new Dictionary<string, string> { ["NODE_ENV"] = "production" };
        }));

        engine.Evaluate("""
            typeof process === 'object' && process.env && process.env.NODE_ENV === 'production'
                ? 'production build'
                : 'development build'
            """).AsString().Should().Be("production build");

        // The same script on an engine that did not opt in must not throw — which is why the idiom starts
        // with a typeof.
        new Engine().Evaluate("""
            typeof process === 'object' && process.env && process.env.NODE_ENV === 'production'
                ? 'production build'
                : 'development build'
            """).AsString().Should().Be("development build");
    }

    [Test]
    public void AProcessGlobalTheHostRegisteredItselfWins()
    {
        var before = new Engine(options => options
            .Configure(e => e.SetValue("process", "host's own"))
            .UseNodeProcess());

        var after = new Engine(options => options
            .UseNodeProcess()
            .Configure(e => e.SetValue("process", "host's own")));

        before.Evaluate("process").AsString().Should().Be("host's own");
        after.Evaluate("process").AsString().Should().Be("host's own");
    }
}
