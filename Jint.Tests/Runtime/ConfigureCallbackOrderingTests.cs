#nullable enable

using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// A callback registered with <c>Options.Configure</c> runs from <c>Options.Apply</c>, in the middle of the
/// constructor. What it may reach, and what it may still change, is one ordering contract:
/// https://github.com/sebastienros/jint/issues/3581, /3582 and /3583.
/// </summary>
public class ConfigureCallbackOrderingTests
{
    private sealed class MarkerConverter : ObjectConverter
    {
        public override bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
        {
            if (value is Uri)
            {
                result = "converted";
                return true;
            }

            result = null;
            return false;
        }

        protected internal override Type[]? HandledTypes => [typeof(Uri)];
    }

    // https://github.com/sebastienros/jint/issues/3581 - running script from a callback is refused by name.

    [Test]
    public void EvaluatingSourceFromAConfigureCallbackIsRefusedByName()
    {
        var exception = Caught.Exception(() => new Engine(options => options.Configure(e => e.Evaluate("1+1"))));

        exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("Options.Configure");
    }

    [Test]
    public void EvaluatingAPreparedScriptFromAConfigureCallbackIsRefusedByName()
    {
        var prepared = Engine.PrepareScript("1+1");

        var exception = Caught.Exception(() => new Engine(options => options.Configure(e => e.Evaluate(prepared))));

        exception.Should().BeOfType<InvalidOperationException>();
    }

    [Test]
    public void InvokingAFunctionFromAConfigureCallbackIsRefusedByName()
    {
        var exception = Caught.Exception(() => new Engine(options => options.Configure(e => e.Invoke("parseInt", "42"))));

        exception.Should().BeOfType<InvalidOperationException>();
    }

    [Test]
    public void EvaluatingSourceFromTheEngineConstructionCallbackIsRefusedByName()
    {
        var exception = Caught.Exception(() => new Engine((Action<Engine, Options>) ((e, _) => e.Evaluate("1+1"))));

        exception.Should().BeOfType<InvalidOperationException>();
    }

    [Test]
    public void ReachingTheEnginesConstraintsFromAConfigureCallbackIsRefusedByName()
    {
        var exception = Caught.Exception(() => new Engine(options =>
        {
            options.LimitStatements(5);
            options.Configure(e => e.Constraints.Find<Jint.Constraints.MaxStatementsConstraint>());
        }));

        exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("Options.Configure");
    }

    [Test]
    public void RegisteringAValueFromAConfigureCallbackStillWorks()
    {
        var engine = new Engine(options => options.Configure(e => e.SetValue("host", 42)));

        engine.Evaluate("host").AsInteger().Should().Be(42);
    }

    // https://github.com/sebastienros/jint/issues/3582 - a profile declared from a callback is refused.

    [Test]
    public void AnUntrustedProfileDeclaredFromAConfigureCallbackIsRefusedByName()
    {
        var exception = Caught.Exception(() => new Engine(options =>
        {
            options.Configure(_ => options.ForUntrustedCode(CreateLimits()));
        }));

        exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("ForUntrustedCode");
    }

    [Test]
    public void AnUntrustedProfileDeclaredBeforeConstructionSurvivesAConfigureCallback()
    {
        var engine = new Engine(options =>
        {
            options.ForUntrustedCode(CreateLimits());
            options.Configure(e => e.SetValue("host", 1));
        });

        engine.Evaluate("typeof System").AsString().Should().Be("undefined");
        engine.Invoking(e => e.Execute("var i = 0; while (true) { i++; }"))
            .Should().Throw<JintException>();
    }

    [Test]
    public void AnUntrustedEngineIgnoresTheLimitItsConfigureCallbackTriedToRaise()
    {
        var engine = new Engine(options =>
        {
            options.ForUntrustedCode(CreateLimits());
            options.Configure(e => e.Options.LimitStatements(int.MaxValue));
        });

        engine.Invoking(e => e.Execute("var i = 0; while (i < 1000000) { i++; }"))
            .Should().Throw<StatementsCountOverflowException>();
    }

    private static UntrustedCodeLimits CreateLimits() => new()
    {
        TimeoutInterval = TimeSpan.FromSeconds(5),
        MaxStatements = 1000,
        MemoryLimit = 16_000_000,
        MaxRecursionDepth = 64,
        MaxArraySize = 10_000,
        RegexTimeout = TimeSpan.FromMilliseconds(100),
        PromiseTimeout = TimeSpan.FromMilliseconds(100),
        MaxOperationDuration = TimeSpan.FromSeconds(10),
    };

    // https://github.com/sebastienros/jint/issues/3583 - what a callback writes is what the engine keeps.

    [Test]
    public void AnObjectConverterAddedFromAConfigureCallbackIsConsulted()
    {
        var engine = new Engine(options =>
        {
            options.Configure(_ => options.AddObjectConverter(new MarkerConverter()));
        });
        engine.SetValue("uri", new Uri("http://example.com/"));

        engine.Evaluate("uri").AsString().Should().Be("converted");
    }

    [Test]
    public void StrictModeSetFromAConfigureCallbackIsHonoured()
    {
        var engine = new Engine(options =>
        {
            options.Configure(_ => options.Strict = true);
        });

        engine.Evaluate("(function(){ return this === undefined; })()").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AStatementLimitSetFromAConfigureCallbackIsEnforced()
    {
        var engine = new Engine(options =>
        {
            options.Configure(_ => options.LimitStatements(5));
        });

        engine.Invoking(e => e.Execute("var i = 0; while (i < 1000) { i++; }"))
            .Should().Throw<StatementsCountOverflowException>();
    }

    [Test]
    public void EnumConversionSetFromAConfigureCallbackIsHonoured()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Configure(_ => options.Interop.EnumConversion = EnumConversionMode.String);
        });
        engine.SetValue("day", DayOfWeek.Monday);

        engine.Evaluate("day").AsString().Should().Be("Monday");
    }

    [Test]
    public void CoverageEnabledFromAConfigureCallbackIsCollected()
    {
        var engine = new Engine(options =>
        {
            options.Configure(_ => options.Coverage.Enabled = true);
        });

        engine.Execute("var i = 0; i++;");

        engine.Diagnostics.GetCoverage().Should().NotBeNull();
    }
}
