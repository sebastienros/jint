#nullable enable

using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// <a href="https://tc39.es/ecma262/#sec-IsHTMLDDA-internal-slot">Annex B.3.6's <c>[[IsHTMLDDA]]</c></a>
/// internal slot, as the three behaviours it grants rather than as one class that happens to have them.
/// </summary>
/// <remarks>
/// The slot has two bearers — this assembly's <c>IsHTMLDDA</c> (test262's <c>$262.IsHTMLDDA</c>) and
/// <c>Jint.Browser</c>'s <c>HTMLAllCollection</c> — and they share no base class beyond
/// <see cref="ObjectInstance"/>, so the three behaviours belong to <c>ObjectInstance</c> and the flag rather
/// than to either of them. What this pins is that an object declaring the slot gets all three without
/// implementing any of them, and that an ordinary object gets none.
/// </remarks>
public class HtmlDdaSlotTests
{
    [Test]
    public void TheSlotAloneDecidesTypeofToBooleanAndLooseEquality()
    {
        var engine = new Engine();
        engine.SetValue("dda", (JsValue) new Jint.Native.IsHTMLDDA(engine, engine.Realm));
        engine.SetValue("plain", new JsObject(engine));

        // typeof, from the flag — the object is callable, so without the slot it would answer "function".
        engine.Evaluate("typeof dda").AsString().Should().Be("undefined");
        engine.Evaluate("typeof plain").AsString().Should().Be("object");

        // ToBoolean, which is the whole point of the slot: `if (document.all)` takes its else branch.
        engine.Evaluate("Boolean(dda)").AsBoolean().Should().BeFalse();
        engine.Evaluate("!dda").AsBoolean().Should().BeTrue();
        engine.Evaluate("dda ? 'then' : 'else'").AsString().Should().Be("else");
        engine.Evaluate("Boolean(plain)").AsBoolean().Should().BeTrue();

        // Loose equality against a *variable*, which is the generic IsLooselyEqual path rather than the
        // build-time fusion GuardFusionTests covers. Both directions, both operands.
        engine.Evaluate("((a, b) => a == b)(dda, undefined)").AsBoolean().Should().BeTrue();
        engine.Evaluate("((a, b) => a == b)(dda, null)").AsBoolean().Should().BeTrue();
        engine.Evaluate("((a, b) => a == b)(undefined, dda)").AsBoolean().Should().BeTrue();
        engine.Evaluate("((a, b) => a == b)(null, dda)").AsBoolean().Should().BeTrue();
        engine.Evaluate("((a, b) => a != b)(dda, null)").AsBoolean().Should().BeFalse();
        engine.Evaluate("((a, b) => a == b)(plain, null)").AsBoolean().Should().BeFalse();

        // And nothing else moves: strict equality, ?? and ?. all treat it as the object it is.
        engine.Evaluate("dda === undefined").AsBoolean().Should().BeFalse();
        engine.Evaluate("dda === null").AsBoolean().Should().BeFalse();
        engine.Evaluate("((a, b) => a === b)(dda, undefined)").AsBoolean().Should().BeFalse();
        engine.Evaluate("(dda ?? 'fallback') === dda").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof dda?.toString").AsString().Should().Be("function");
        engine.Evaluate("dda == dda").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The capability the seam exists for: an object that <em>declares</em> the slot and implements none of
    /// the three behaviours gets all three. <c>Jint.Browser</c>'s <c>HTMLAllCollection</c> is the reason —
    /// it derives from <c>ArrayLikeObject</c>, so it shares no base with <c>IsHTMLDDA</c> and could only have
    /// reproduced Annex B by hand.
    /// </summary>
    [Test]
    public void ADeclaringObjectImplementsNoneOfIt()
    {
        var engine = new Engine();
        engine.SetValue("bearer", new Bearer(engine));

        engine.Evaluate("typeof bearer").AsString().Should().Be("undefined");
        engine.Evaluate("Boolean(bearer)").AsBoolean().Should().BeFalse();
        engine.Evaluate("bearer == null").AsBoolean().Should().BeTrue();
        engine.Evaluate("((a, b) => a == b)(bearer, undefined)").AsBoolean().Should().BeTrue();
        engine.Evaluate("bearer === undefined").AsBoolean().Should().BeFalse();

        // Not callable: the slot and [[Call]] are declared separately, and this one declares only the slot.
        engine.Evaluate("(() => { try { bearer(); return 'no throw'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    private sealed class Bearer : ObjectInstance
    {
        internal Bearer(Engine engine) : base(engine) => DeclareIsHtmlDda();
    }
}
