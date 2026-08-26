#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Json;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Jint guards <em>operations</em> against concurrent use and does not guard <em>value construction</em>.
/// These pin where that line is: which public APIs taking an <see cref="Engine"/> refuse a second thread and
/// which build the value anyway, and that turning host-contract verification on catches the second kind
/// without an always-on check on a bulk path.
/// </summary>
public class HostValueConstructionThreadTests
{
    private static readonly JsObjectLayout Layout = new("id", "name");

    /// <summary>
    /// Runs <paramref name="build"/> on a second thread while the engine is inside a host call on this one,
    /// and reports what happened. The rendezvous is a two-way handshake rather than a sleep: the host call
    /// signals that it is running, the second thread answers, and only then does the host call return.
    /// </summary>
    private static string OutcomeOnASecondThread(Action<Engine> build)
    {
        var engine = new Engine();
        var outcome = "the second thread never ran";

        using var insideTheHostCall = new ManualResetEventSlim(false);
        using var secondThreadAnswered = new ManualResetEventSlim(false);

        engine.SetValue("block", new Action(() =>
        {
            insideTheHostCall.Set();
            secondThreadAnswered.Wait(TestBudgets.WedgeCeiling);
        }));

        var second = new Thread(() =>
        {
            insideTheHostCall.Wait(TestBudgets.WedgeCeiling);
            try
            {
                build(engine);
                outcome = "built";
            }
            catch (Exception exception)
            {
                outcome = exception.GetType().Name;
            }

            secondThreadAnswered.Set();
        })
        {
            IsBackground = true,
        };

        second.Start();
        engine.Evaluate("block()");
        second.Join(TestBudgets.WedgeCeiling);
        return outcome;
    }

    private static Action<Engine> Builder(string api) => api switch
    {
        "JsArray" => static engine => _ = new JsArray(engine, 4),
        "JsObject.Create" => static engine => _ = JsObject.Create(engine, Layout, new JsValue?[] { 1, "a" }),
        "JsObject.CreateFromEntries" => static engine => _ = JsObject.CreateFromEntries(
            engine,
            new[] { new KeyValuePair<string, JsValue>("a", 1) }),
        _ => throw new ArgumentOutOfRangeException(nameof(api), api, "unknown construction API"),
    };

    [TestCase("JsArray")]
    [TestCase("JsObject.Create")]
    [TestCase("JsObject.CreateFromEntries")]
    public void ConstructionIsOutsideTheConcurrencyGuardAndVerificationIsWhatCatchesIt(string api)
    {
        var outcome = OutcomeOnASecondThread(Builder(api));

        if (HostContractVerificationSwitch.Enabled)
        {
            outcome.Should().Be(
                "InvalidOperationException",
                "host-contract verification is where this class of violation is reported");
        }
        else
        {
            outcome.Should().Be(
                "built",
                "construction takes no reservation, which is the line this test exists to write down");
        }
    }

    [TestCase("Engine.Evaluate")]
    [TestCase("JsValue.FromObject")]
    [TestCase("JsonSerializer.Serialize")]
    [TestCase("JsonParser.Parse")]
    public void TheseEntriesRefuseASecondThreadWhicheverWayVerificationIsSet(string api)
    {
        Action<Engine> enter = api switch
        {
            "Engine.Evaluate" => static engine => engine.Evaluate("1 + 1"),
            "JsValue.FromObject" => static engine => JsValue.FromObject(engine, new { a = 1 }),
            "JsonSerializer.Serialize" => static engine => new JsonSerializer(engine).Serialize(JsValue.Null),
            "JsonParser.Parse" => static engine => new JsonParser(engine).Parse("{\"a\":1}"),
            _ => throw new ArgumentOutOfRangeException(nameof(api), api, "unknown entry"),
        };

        OutcomeOnASecondThread(enter).Should().Be("InvalidOperationException");
    }

    [Test]
    public void TheVerifierNamesTheTypeThatWasBuilt()
    {
        if (!HostContractVerificationSwitch.Enabled)
        {
            Assert.Ignore("host-contract verification is off");
        }

        var engine = new Engine();
        Exception? captured = null;

        using var insideTheHostCall = new ManualResetEventSlim(false);
        using var secondThreadAnswered = new ManualResetEventSlim(false);

        engine.SetValue("block", new Action(() =>
        {
            insideTheHostCall.Set();
            secondThreadAnswered.Wait(TestBudgets.WedgeCeiling);
        }));

        var second = new Thread(() =>
        {
            insideTheHostCall.Wait(TestBudgets.WedgeCeiling);
            captured = Caught.Exception(() => new JsArray(engine, 4));
            secondThreadAnswered.Set();
        })
        {
            IsBackground = true,
        };

        second.Start();
        engine.Evaluate("block()");
        second.Join(TestBudgets.WedgeCeiling);

        captured.Should().BeOfType<InvalidOperationException>();
        captured!.Message.Should().Contain("JsArray").And.Contain("was using that engine");
    }

    [Test]
    public void AnIdleEngineAcceptsAValueBuiltOnAnyThread()
    {
        var engine = new Engine();
        JsValue? built = null;
        Exception? captured = null;

        var other = new Thread(() => captured = Caught.Exception(
            () => built = JsObject.CreateFromEntries(engine, new[] { new KeyValuePair<string, JsValue>("a", 1) })))
        {
            IsBackground = true,
        };

        other.Start();
        other.Join(TestBudgets.WedgeCeiling);

        captured.Should().BeNull("a host building a value between turns is the legitimate case");
        engine.SetValue("row", built!);
        engine.Evaluate("row.a").AsNumber().Should().Be(1);
    }

    [Test]
    public void TheOwningThreadMayBuildValuesFromInsideAHostCallback()
    {
        var engine = new Engine();
        Exception? captured = null;

        engine.SetValue("build", new Action(() => captured = Caught.Exception(() =>
        {
            _ = new JsArray(engine, 4);
            _ = JsObject.Create(engine, Layout, new JsValue?[] { 1, "a" });
        })));

        engine.Evaluate("build()");
        captured.Should().BeNull("the thread inside the engine is the thread that may build for it");
    }
}
