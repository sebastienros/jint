#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A string the engine hands a host is a value, and a value does not change: script that goes on appending
/// to the binding it came from produces a new string and leaves the host's alone. <c>s += t</c> used to append
/// into the very instance the binding held, so every exit that returned that instance without copying it —
/// a completion value, <see cref="Engine.GetValue(string)"/>, a property read, a descriptor, an argument a
/// built-in passed to a host delegate, a module export — handed the host a string whose text changed under
/// it later (sebastienros/jint#4259). Each test here holds a string, lets script append to where it came
/// from, and checks the held text is what it was.
/// </summary>
public class HostStringImmutabilityTests
{
    /// <summary>Two appends: the first produces a value from one concatenation, the second grows it.</summary>
    private const string BuildS = "var s = 'a'; s += 'b'; s += 'c';";

    [Test]
    public void AnEvaluateResultDoesNotChangeWhenScriptAppendsToItsBinding()
    {
        using var engine = new Engine();

        var result = engine.Evaluate(BuildS + " s");
        engine.Execute("s += 'd'");

        result.AsString().Should().Be("abc");
        engine.Evaluate("s").AsString().Should().Be("abcd");
    }

    [Test]
    public void AGetValueResultDoesNotChangeWhenScriptAppendsToItsBinding()
    {
        using var engine = new Engine();
        engine.Execute(BuildS);

        var held = engine.GetValue("s");
        engine.Execute("s += 'd'");

        held.AsString().Should().Be("abc");
        engine.GetValue("s").AsString().Should().Be("abcd");
    }

    [Test]
    public void APropertyReadDoesNotChangeWhenScriptAppendsToTheProperty()
    {
        using var engine = new Engine();
        var obj = engine.Evaluate("var o = { p: 'x' }; o.p += 'y'; o.p += 'z'; o").AsObject();

        var held = obj.Get("p");
        engine.Execute("o.p += '!'");

        held.AsString().Should().Be("xyz");
        obj.Get("p").AsString().Should().Be("xyz!");
    }

    [Test]
    public void ADescriptorValueDoesNotChangeWhenScriptAppendsToTheProperty()
    {
        using var engine = new Engine();
        var obj = engine.Evaluate("var o = { p: 'x' }; o.p += 'y'; o.p += 'z'; o").AsObject();

        var held = obj.GetOwnProperty("p").Value;
        engine.Execute("o.p += '!'");

        held.AsString().Should().Be("xyz");
    }

    [Test]
    public void AnArrayElementDoesNotChangeWhenScriptAppendsToTheElement()
    {
        using var engine = new Engine();
        var array = engine.Evaluate("var a = ['x']; a[0] += 'y'; a[0] += 'z'; a").AsArray();

        var held = array[0];
        engine.Execute("a[0] += '!'");

        held.AsString().Should().Be("xyz");
        array[0].AsString().Should().Be("xyz!");
    }

    [Test]
    public void AnInvokeResultDoesNotChangeWhenScriptAppendsToWhatTheFunctionReturned()
    {
        using var engine = new Engine();
        engine.Execute(BuildS + " var o = { p: 'x' }; o.p += 'y'; function getS() { return s; } function getP() { return o.p; }");

        var fromBinding = engine.Invoke("getS");
        var fromProperty = engine.Invoke("getP");
        engine.Execute("s += 'd'; o.p += 'z';");

        fromBinding.AsString().Should().Be("abc");
        fromProperty.AsString().Should().Be("xy");
    }

    [Test]
    public void ADelegateArgumentDoesNotChangeWhenScriptAppendsToWhatItPassed()
    {
        using var engine = new Engine();
        var held = new List<JsValue>();
        engine.SetValue("keep", new Action<JsValue>(held.Add));

        // A direct call, and a built-in calling the delegate with an element it read off an array itself.
        engine.Execute(BuildS + " keep(s); s += 'd';");
        engine.Execute("var a = ['x']; a[0] += 'y'; a[0] += 'z'; a.forEach(function (v) { keep(v); }); a.forEach(keep); a[0] += '!';");

        held.Select(v => v.AsString()).Should().Equal("abc", "xyz", "xyz");
    }

    [Test]
    public void AValueAssignedToAHostPropertyDoesNotChangeWhenScriptAppendsToItsSource()
    {
        using var engine = new Engine(options => options.Interop.AllowWrite = true);
        var holder = new Holder();
        engine.SetValue("holder", holder);

        engine.Execute(BuildS + " holder.Value = s; s += 'd';");

        holder.Value.AsString().Should().Be("abc");
    }

    [Test]
    public void AModuleExportDoesNotChangeWhenTheModuleAppendsToItsBinding()
    {
        using var engine = new Engine();
        engine.Modules.Add("m", "export let s = 'a'; s += 'b'; s += 'c'; export function grow(x) { s += x; }");
        var ns = engine.Modules.Import("m");

        var held = ns.Get("s");
        engine.Invoke(ns.Get("grow"), "d");

        held.AsString().Should().Be("abc");
        ns.Get("s").AsString().Should().Be("abcd");
    }

    /// <summary>
    /// A primitive may be handed from one engine to another. The receiving engine appending to it must not
    /// change the string the first engine, or its host, still holds.
    /// </summary>
    [Test]
    public void AnotherEngineAppendingToAStringDoesNotChangeItForTheEngineThatMadeIt()
    {
        using var first = new Engine();
        using var second = new Engine();

        var held = first.Evaluate(BuildS + " s");
        second.SetValue("s", held);
        second.Execute("s += 'd'; s += 'e';");

        held.AsString().Should().Be("abc");
        first.Evaluate("s").AsString().Should().Be("abc");
        second.Evaluate("s").AsString().Should().Be("abcde");
    }

    /// <summary>
    /// Two engines, each on its own thread, appending to the one string a host gave both. Each must end
    /// with its own text, and the shared one must be left as it was.
    /// </summary>
    [Test]
    public async Task EnginesAppendingConcurrentlyToOneSharedStringEachBuildTheirOwnText()
    {
        const int Appends = 20_000;

        using var origin = new Engine();
        var shared = origin.Evaluate(BuildS + " s");

        using var start = new Barrier(2);
        string Build(char suffix)
        {
            using var engine = new Engine();
            engine.SetValue("s", shared);
            engine.SetValue("n", Appends);
            start.SignalAndWait(TestBudgets.WedgeCeiling);
            return engine.Evaluate($"for (var i = 0; i < n; i++) {{ s += '{suffix}'; }} s").AsString();
        }

        string? left = null;
        string? right = null;
        await Task.WhenAll(
            DedicatedThread.RunAsync(() => left = Build('x')),
            DedicatedThread.RunAsync(() => right = Build('y')));

        left.Should().Be("abc" + new string('x', Appends));
        right.Should().Be("abc" + new string('y', Appends));
        shared.AsString().Should().Be("abc");
    }

    /// <summary>
    /// A string the host holds can be read on another thread while its engine keeps appending to the binding
    /// it came from: nothing the engine does afterwards writes to what that string reads.
    /// </summary>
    [Test]
    public async Task AHeldStringReadsTheSameWhileItsEngineKeepsAppending()
    {
        using var engine = new Engine();
        var held = engine.Evaluate(BuildS + " s");

        var stop = 0;
        var reads = 0;
        string? mismatch = null;
        var reader = DedicatedThread.RunAsync(() =>
        {
            while (Volatile.Read(ref stop) == 0 || reads == 0)
            {
                var text = held.ToString();
                if (text != "abc" || held is JsString { Length: not 3 })
                {
                    mismatch = text;
                    return;
                }

                reads++;
            }
        });

        engine.Execute("for (var i = 0; i < 100000; i++) { s += 'x'; }");
        Volatile.Write(ref stop, 1);
        await reader;

        mismatch.Should().BeNull();
        held.AsString().Should().Be("abc");
        engine.Evaluate("s.length").AsNumber().Should().Be(100_003);
    }

    /// <summary>
    /// A host's own string type is appended to through the base implementation, which produces a new value
    /// and leaves the host's instance as it was.
    /// </summary>
    [Test]
    public void AHostStringSubclassIsLeftAsItWasByAppends()
    {
        using var engine = new Engine();
        var hosted = new HostText("abc");
        engine.SetValue("s", hosted);

        engine.Execute("s += 'd'; s += 'e'; var t = s; s += 'f';");

        hosted.ToString().Should().Be("abc");
        engine.Evaluate("t").AsString().Should().Be("abcde");
        engine.Evaluate("s").AsString().Should().Be("abcdef");
    }

    public sealed class Holder
    {
        public JsValue Value { get; set; } = JsValue.Undefined;
    }

    private sealed class HostText(string text) : LazyJsString(text.Length)
    {
        protected override string Materialize() => text;
    }
}
