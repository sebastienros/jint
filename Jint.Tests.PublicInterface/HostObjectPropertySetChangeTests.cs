using System;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers what happens when the own-property set of a host <see cref="Jint.Native.Object.ObjectInstance"/>
/// subclass changes <em>behind the engine's back</em> — the native state a projecting host reflects gains or
/// loses a member without any JavaScript-visible write.
///
/// <para>
/// This is the shape the member-read inline caches cannot version. The engine bumps an internal counter from
/// its own property-bag mutators and the caches validate against it, but a host that stores its properties
/// itself never touches that bag, so the counter is frozen for the object's whole lifetime and cannot stand
/// in for "the own-property set is unchanged". Every read of such a receiver — and of such a prototype — must
/// therefore ask the host again rather than trust a cached answer.
/// </para>
///
/// <para>
/// Each test drives the reads through <b>one</b> member expression evaluated repeatedly, because the caches
/// are per-AST-node: a fresh <c>Evaluate</c> per read would compile a fresh node and never warm anything.
/// </para>
///
/// <para>
/// The last pair repeats the receiver-side cases for a host that overrides
/// <c>ObjectInstance.TryGetOwnPropertyValue</c>. That host takes a different lane — the own-property question
/// is answered by the hook rather than by a discarded <c>GetOwnProperty</c> probe — so the ordering has to
/// hold there on its own evidence: the question is still asked, and still asked <em>before</em> the
/// prototype-method cache is consulted, on every single read.
/// </para>
/// </summary>
public class HostObjectPropertySetChangeTests
{
    [Fact]
    public void AProjectedPropertyAppearingOnAReceiverShadowsThePrototypeOnTheNextRead()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine);
        engine.SetValue("host", host);
        engine.SetValue("projectShared", new Action(() => host.Project("shared", "from-host")));

        var seen = engine.Evaluate(
            """
            Object.prototype.shared = 'from-prototype';

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(host.shared);
                if (i === 0) {
                    projectShared();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("from-prototype,from-host,from-host");
    }

    [Fact]
    public void AProjectedMethodAppearingOnAReceiverShadowsThePrototypeOnTheNextCall()
    {
        // The member-call callee lane shares the same non-plain-receiver completion, so it needs the same guard.
        var engine = new Engine();
        var host = new ProjectedHostObject(engine);
        engine.SetValue("host", host);
        engine.SetValue(
            "projectGreet",
            new Action(() => host.Project("greet", engine.Evaluate("(function () { return 'from-host'; })"))));

        var seen = engine.Evaluate(
            """
            Object.prototype.greet = function () { return 'from-prototype'; };

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(host.greet());
                if (i === 0) {
                    projectGreet();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("from-prototype,from-host,from-host");
    }

    [Fact]
    public void AProjectedPropertyDisappearingFromAReceiverFallsBackToThePrototype()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine).Project("shared", "from-host");
        engine.SetValue("host", host);
        engine.SetValue("unprojectShared", new Action(() => host.Unproject("shared")));

        var seen = engine.Evaluate(
            """
            Object.prototype.shared = 'from-prototype';

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(host.shared);
                if (i === 0) {
                    unprojectShared();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("from-host,from-prototype,from-prototype");
    }

    /// <summary>
    /// The other per-node caches, from the outside. The own-property and shape lanes are gated on internal
    /// storage flags a host subclass cannot carry, and the wrapper lane on the receiver's exact type, so none
    /// of them ever holds a descriptor produced by a host — which is observable here, because this host builds
    /// a fresh descriptor per probe and a cached one would freeze the value.
    /// </summary>
    [Fact]
    public void AProjectedPropertyChangingValueOnAReceiverIsSeenByTheNextRead()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine).Project("value", "v1");
        engine.SetValue("host", host);
        engine.SetValue("reproject", new Action(() => host.Project("value", "v2")));

        var seen = engine.Evaluate(
            """
            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(host.value);
                if (i === 0) {
                    reproject();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("v1,v2,v2");
    }

    [Fact]
    public void AHostUsedAsAPrototypeIsRereadWhenItsProjectionChanges()
    {
        // The holder side of the same cache. Here the receiver is an ordinary script object whose version the
        // engine does own; it is the prototype that projects its members, and its version is the frozen one.
        var engine = new Engine();
        var hostPrototype = new ProjectedHostObject(engine).Project("shared", "v1");
        engine.SetValue("hostPrototype", hostPrototype);
        engine.SetValue("reproject", new Action(() => hostPrototype.Project("shared", "v2")));

        var seen = engine.Evaluate(
            """
            var obj = Object.create(hostPrototype);

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(obj.shared);
                if (i === 0) {
                    reproject();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("v1,v2,v2");
    }

    [Fact]
    public void AHostUsedAsAPrototypeIsRereadWhenAProjectedMemberDisappears()
    {
        var engine = new Engine();
        var hostPrototype = new ProjectedHostObject(engine).Project("shared", "from-host-prototype");
        engine.SetValue("hostPrototype", hostPrototype);
        engine.SetValue("unprojectShared", new Action(() => hostPrototype.Unproject("shared")));

        var seen = engine.Evaluate(
            """
            var obj = Object.create(hostPrototype);

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(obj.shared === undefined ? 'absent' : obj.shared);
                if (i === 0) {
                    unprojectShared();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("from-host-prototype,absent,absent");
    }

    [Fact]
    public void AProjectedPropertyAppearingOnAValueAnsweringReceiverShadowsThePrototypeOnTheNextRead()
    {
        // The ordering guard for the descriptor-free lane. A host that answers its own reads never has a
        // GetOwnProperty probe standing between the read and the prototype-method cache, so if that lane ever
        // consulted the cache first, the warm entry from iteration 0 would keep answering 'from-prototype'
        // forever and this would read "from-prototype,from-prototype,from-prototype".
        var engine = new Engine();
        var host = new ValueAnsweringHostObject(engine);
        engine.SetValue("host", host);
        engine.SetValue("projectShared", new Action(() => host.Project("shared", "from-host")));

        var seen = engine.Evaluate(
            """
            Object.prototype.shared = 'from-prototype';

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(host.shared);
                if (i === 0) {
                    projectShared();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("from-prototype,from-host,from-host");
    }

    [Fact]
    public void AProjectedPropertyDisappearingFromAValueAnsweringReceiverFallsBackToThePrototype()
    {
        // The other direction, and the one that pins what a false from the hook is taken to mean: the engine
        // does not re-probe after it, so the fall-back to the prototype has to come from the hook's answer
        // alone. A false that were treated as "ask GetOwnProperty" instead would still pass; a false that were
        // ignored, or an own answer cached across reads, would not.
        var engine = new Engine();
        var host = (ValueAnsweringHostObject) new ValueAnsweringHostObject(engine).Project("shared", "from-host");
        engine.SetValue("host", host);
        engine.SetValue("unprojectShared", new Action(() => host.Unproject("shared")));

        var seen = engine.Evaluate(
            """
            Object.prototype.shared = 'from-prototype';

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(host.shared);
                if (i === 0) {
                    unprojectShared();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("from-host,from-prototype,from-prototype");
    }

    [Fact]
    public void AProjectedMethodAppearingOnAValueAnsweringReceiverShadowsThePrototypeOnTheNextCall()
    {
        // The member-call callee lane shares the same non-plain-receiver completion, and therefore the same
        // hook branch within it.
        var engine = new Engine();
        var host = new ValueAnsweringHostObject(engine);
        engine.SetValue("host", host);
        engine.SetValue(
            "projectGreet",
            new Action(() => host.Project("greet", engine.Evaluate("(function () { return 'from-host'; })"))));

        var seen = engine.Evaluate(
            """
            Object.prototype.greet = function () { return 'from-prototype'; };

            var seen = [];
            for (var i = 0; i < 3; i++) {
                seen.push(host.greet());
                if (i === 0) {
                    projectGreet();
                }
            }
            seen.join(',');
            """);

        seen.Should().Be("from-prototype,from-host,from-host");
    }

    [Fact]
    public void AReceiverThatGainsAProjectedPropertyStillReportsItThroughTheObjectProtocol()
    {
        // The same change seen from outside the member-read lane, so a failure of the lane is distinguishable
        // from a host fixture that simply does not work.
        var engine = new Engine();
        var host = new ProjectedHostObject(engine);
        engine.SetValue("host", host);

        engine.Execute("Object.prototype.shared = 'from-prototype';");
        engine.Evaluate("host.shared").Should().Be("from-prototype");

        host.Project("shared", "from-host");

        engine.Evaluate("host.shared").Should().Be("from-host");
        engine.Evaluate("host['share' + 'd']").Should().Be("from-host");
        host.Get("shared").Should().Be("from-host");
        engine.Evaluate("Object.getOwnPropertyDescriptor(host, 'shared').value").Should().Be("from-host");
        engine.Evaluate("Object.prototype.hasOwnProperty.call(host, 'shared')").Should().Be(JsBoolean.True);
    }
}
