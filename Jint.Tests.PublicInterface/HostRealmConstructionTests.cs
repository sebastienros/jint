#nullable enable

using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public sealed class HostRealmConstructionTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void AFailedRealmConstructionRestoresThePrincipalRealm(bool failInIntrinsics)
    {
        var host = new ObservingHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var global = engine.Global;
        var intrinsics = engine.Intrinsics;
        var failure = new InvalidOperationException("Realm construction failed");
        Action<Realm> fail = _ => throw failure;
        if (failInIntrinsics)
        {
            host.BeforeIntrinsics = fail;
        }
        else
        {
            host.BeforeGlobal = fail;
        }

        Caught.Exception(() => host.CreateAdditionalRealm()).Should().BeSameAs(failure);
        host.BeforeIntrinsics = null;
        host.BeforeGlobal = null;

        engine.Global.Should().BeSameAs(global);
        engine.Intrinsics.Should().BeSameAs(intrinsics);
        engine.Evaluate("Object.getPrototypeOf({}) === Object.prototype").Should().BeTrue();
        var next = host.CreateAdditionalRealm();
        next.GlobalObject.Should().NotBeSameAs(global);
        engine.Global.Should().BeSameAs(global);
        engine.Intrinsics.Should().BeSameAs(intrinsics);
    }

    [TestCase("none")]
    [TestCase("intrinsics")]
    [TestCase("global")]
    public void ANestedConstructionRestoresTheOuterRealmBeforeItsFactoryContinues(string failurePhase)
    {
        var host = new ObservingHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var global = engine.Global;
        var intrinsics = engine.Intrinsics;
        var failure = new InvalidOperationException("Nested realm construction failed");
        Intrinsics? afterNested = null;
        Exception? observedFailure = null;
        Realm? nested = null;

        host.BeforeGlobal = _ =>
        {
            host.BeforeGlobal = null;
            if (failurePhase == "intrinsics")
            {
                host.BeforeIntrinsics = _ => throw failure;
            }
            else if (failurePhase == "global")
            {
                host.BeforeGlobal = _ => throw failure;
            }

            observedFailure = Caught.Exception(() => nested = host.CreateAdditionalRealm());
            host.BeforeIntrinsics = null;
            host.BeforeGlobal = null;
            afterNested = engine.Intrinsics;
        };

        var outer = host.CreateAdditionalRealm();

        afterNested.Should().BeSameAs(outer.Intrinsics, "the outer factory resumes in its own construction realm");
        if (failurePhase == "none")
        {
            observedFailure.Should().BeNull();
            nested!.Intrinsics.Should().NotBeSameAs(outer.Intrinsics);
        }
        else
        {
            observedFailure.Should().BeSameAs(failure);
        }
        engine.Global.Should().BeSameAs(global);
        engine.Intrinsics.Should().BeSameAs(intrinsics);
        engine.Evaluate("Object.getPrototypeOf({}) === Object.prototype").Should().BeTrue();
    }

    private sealed class ObservingHost : Host
    {
        public Action<Realm>? BeforeIntrinsics { get; set; }
        public Action<Realm>? BeforeGlobal { get; set; }

        public Realm CreateAdditionalRealm() => base.CreateRealm();

        protected override void CreateIntrinsics(Realm realmRec)
        {
            BeforeIntrinsics?.Invoke(realmRec);
            base.CreateIntrinsics(realmRec);
        }

        protected override ObjectInstance CreateGlobalObject(Realm realm)
        {
            BeforeGlobal?.Invoke(realm);
            return base.CreateGlobalObject(realm);
        }
    }
}
