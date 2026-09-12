#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>The explicitly installed Web API surface and global event state of additional realms.</summary>
public sealed class RealmWebApiInstallationTests
{
    [Test]
    public void EachInstalledRealmGetsItsOwnLazyInterfacesAndSelf()
    {
        var (engine, host) = Create(WebApiFeatures.Events | WebApiFeatures.GlobalEvents);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            var principalSelf = engine.Realm.GlobalObject.GetOwnProperty("self");
            WebApiRegistration.InstallInRealm(engine, other);
            WebApiRegistration.InstallInRealm(engine, engine.Realm);

            var principalEvent = engine.Evaluate("Event");
            var otherEvent = In(engine, other, "Event");

            otherEvent.Should().NotBeSameAs(principalEvent);
            In(engine, other, "Event.prototype").Should().NotBeSameAs(engine.Evaluate("Event.prototype"));
            In(engine, other, "self === globalThis").AsBoolean().Should().BeTrue();
            engine.Evaluate("self === globalThis").AsBoolean().Should().BeTrue();
            engine._webApi!.InstalledSelf.Should().BeSameAs(principalSelf,
                "a secondary Window-shaped self must not replace the descriptor a principal worker may swap");
            engine._secondaryWebApiRealms!.TryGet(other, out var otherState).Should().BeTrue();
            otherState!.InstalledSelf.Should().BeSameAs(other.GlobalObject.GetOwnProperty("self"));

            // Reading B first still materializes B's descriptor from B's captured realm.
            var third = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, third);
            var thirdConstructor = In(engine, third, "ErrorEvent");
            thirdConstructor.Should().NotBeSameAs(engine.Evaluate("ErrorEvent"));
            thirdConstructor.Should().NotBeSameAs(In(engine, other, "ErrorEvent"));
        }
    }

    [Test]
    public void GlobalListenersAndBorrowedEventTargetMethodsResolveTheReceiverRealm()
    {
        var (engine, host) = Create(WebApiFeatures.Events | WebApiFeatures.GlobalEvents);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            engine.SetValue("otherGlobal", other.GlobalObject);

            Assert.Throws<JavaScriptException>(() => engine.Execute("EventTarget.prototype.addEventListener.call(otherGlobal, 'ping', () => {})"))!
                .Message.Should().Contain("Illegal invocation");

            WebApiRegistration.InstallInRealm(engine, other);
            engine.Execute("var principalCount = 0; addEventListener('ping', () => principalCount++);");
            In(engine, other, """
                var otherCount = 0;
                var identity = false;
                EventTarget.prototype.addEventListener.call(globalThis, 'ping', function (event) {
                    otherCount++;
                    identity = event.target === globalThis
                        && event.currentTarget === globalThis
                        && this === globalThis;
                });
                EventTarget.prototype.dispatchEvent.call(globalThis, new Event('ping'));
                """);

            In(engine, other, "otherCount").AsNumber().Should().Be(1);
            In(engine, other, "identity").AsBoolean().Should().BeTrue();
            engine.Evaluate("principalCount").AsNumber().Should().Be(0);

            engine.Execute("dispatchEvent(new Event('ping'))");
            engine.Evaluate("principalCount").AsNumber().Should().Be(1);
            In(engine, other, "otherCount").AsNumber().Should().Be(1);
        }
    }

    [Test]
    public void ReportErrorAndCallbackErrorsUseTheDestinationGlobal()
    {
        var sink = new RecordingSink();
        var (engine, host) = Create(
            WebApiFeatures.Events | WebApiFeatures.GlobalEvents | WebApiFeatures.Reporting | WebApiFeatures.Timers,
            sink);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, other);

            engine.Execute("var principalErrors = 0; addEventListener('error', () => principalErrors++);");
            In(engine, other, """
                var otherErrors = [];
                addEventListener('error', event => otherErrors.push(event.error));
                reportError('reported');
                var target = new EventTarget();
                target.addEventListener('ping', () => { throw 'callback'; });
                target.dispatchEvent(new Event('ping'));
                queueMicrotask(() => { throw 'microtask'; });
                """);

            In(engine, other, "otherErrors.join(',')").AsString().Should().Be("reported,callback,microtask");
            engine.Evaluate("principalErrors").AsNumber().Should().Be(0);
            sink.Reports.Should().HaveCount(3);
        }
    }

    [Test]
    public void PromiseRejectionEventsUseThePromisesRealm()
    {
        var sink = new RecordingSink();
        var (engine, host) = Create(WebApiFeatures.Events | WebApiFeatures.GlobalEvents, sink);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, other);

            engine.Execute("var principalRejections = 0; addEventListener('unhandledrejection', () => principalRejections++);");
            In(engine, other, """
                var rejectionEvents = [];
                addEventListener('unhandledrejection', function (event) {
                    rejectionEvents.push(
                        event.target === globalThis
                        && event.currentTarget === globalThis
                        && this === globalThis
                        && event.promise === rejected
                        && event.promise instanceof Promise
                        && event.reason === 'other');
                });
                addEventListener('rejectionhandled', function (event) {
                    rejectionEvents.push(event.target === globalThis && event.promise === rejected);
                });
                var rejected = Promise.reject('other');
                """);

            In(engine, other, "rejected.catch(() => {});");

            In(engine, other, "rejectionEvents.join(',')").AsString().Should().Be("true,true");
            engine.Evaluate("principalRejections").AsNumber().Should().Be(0);
            sink.Reports.Should().HaveCount(2);
        }
    }

    [Test]
    public void TimerErrorsUseTheSchedulingRealm()
    {
        var clock = new ManualClock();
        var sink = new RecordingSink();
        var (engine, host) = Create(
            WebApiFeatures.Events | WebApiFeatures.GlobalEvents | WebApiFeatures.Timers,
            sink,
            clock);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, other);

            engine.Execute("var principalErrors = 0; addEventListener('error', () => principalErrors++);");
            In(engine, other, """
                var timerErrorWasLocal = false;
                addEventListener('error', function (event) {
                    timerErrorWasLocal = event.target === globalThis
                        && event.currentTarget === globalThis
                        && this === globalThis
                        && event.error === 'timer';
                });
                setTimeout(() => { throw 'timer'; }, 1);
                """);

            clock.Advance(1);
            engine.Tasks.ProcessTasks();

            In(engine, other, "timerErrorWasLocal").AsBoolean().Should().BeTrue();
            engine.Evaluate("principalErrors").AsNumber().Should().Be(0);
            sink.Reports.Should().ContainSingle()
                .Which.CallbackSource.Should().Be(DiagnosticCallbackSource.Timer);
        }
    }

    [Test]
    public void IdleCallbacksUseTheRegistrationRealmForDeadlineAndErrors()
    {
        var sink = new RecordingSink();
        var (engine, host) = Create(
            WebApiFeatures.Events | WebApiFeatures.GlobalEvents | WebApiFeatures.IdleCallback,
            sink);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, other);

            engine.Execute("var principalErrors = 0; addEventListener('error', () => principalErrors++);");
            In(engine, other, """
                var idleDeadlineWasLocal = false;
                var idleErrorWasLocal = false;
                addEventListener('error', function (event) {
                    idleErrorWasLocal = event.target === globalThis
                        && event.currentTarget === globalThis
                        && this === globalThis
                        && event.error === 'idle';
                });
                requestIdleCallback(deadline => {
                    idleDeadlineWasLocal = deadline instanceof IdleDeadline
                        && Object.getPrototypeOf(deadline) === IdleDeadline.prototype;
                    throw 'idle';
                });
                """);

            In(engine, other, "idleDeadlineWasLocal").AsBoolean().Should().BeTrue();
            In(engine, other, "idleErrorWasLocal").AsBoolean().Should().BeTrue();
            engine.Evaluate("principalErrors").AsNumber().Should().Be(0);
            sink.Reports.Should().ContainSingle()
                .Which.CallbackSource.Should().Be(DiagnosticCallbackSource.IdleCallback);
        }
    }

    [Test]
    public void WindowCurrentEventIsKeptPerDestinationRealm()
    {
        var (engine, host) = Create(WebApiFeatures.Events | WebApiFeatures.GlobalEvents);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, other);
            var principalTarget = engine._webApi!.GlobalEventTarget;
            var otherTarget = engine._webApi.GlobalEventTargetFor(other);
            principalTarget.IsWindow = true;
            otherTarget.IsWindow = true;
            engine.Advanced.WithRealm(other, scoped =>
            {
                scoped.SetValue("readPrincipalEvent", new Func<JsValue>(() => principalTarget.CurrentEvent));
                scoped.SetValue("readOtherEvent", new Func<JsValue>(() => otherTarget.CurrentEvent));
            });

            In(engine, other, """
                var event = new Event('ping');
                var otherDuring = false;
                var principalDuring = false;
                addEventListener('ping', () => {
                    otherDuring = readOtherEvent() === event;
                    principalDuring = readPrincipalEvent() !== undefined;
                });
                dispatchEvent(event);
                """);

            In(engine, other, "otherDuring").AsBoolean().Should().BeTrue();
            In(engine, other, "principalDuring").AsBoolean().Should().BeFalse();
            principalTarget.CurrentEvent.Should().Be(JsValue.Undefined);
            otherTarget.CurrentEvent.Should().Be(JsValue.Undefined);
        }
    }

    [Test]
    public void InvalidRealmsAreRejectedBeforeTheirGlobalsChange()
    {
        var (engine, host) = Create(WebApiFeatures.Console);
        using (engine)
        {
            Assert.Throws<ArgumentNullException>(() => WebApiRegistration.InstallInRealm(engine, null!))!
                .ParamName.Should().Be("realm");
            Assert.Throws<ArgumentException>(() => WebApiRegistration.InstallInRealm(engine, new Realm()))!
                .ParamName.Should().Be("realm");

            var foreignHost = new RealmHost();
            using var attachedForeign = new Engine(options => options.UseHostFactory(_ => foreignHost));
            var foreign = foreignHost.CreateAdditionalRealm();
            Assert.Throws<ArgumentException>(() => WebApiRegistration.InstallInRealm(engine, foreign))!
                .ParamName.Should().Be("realm");
            foreign.GlobalObject.HasOwnProperty(WebApiRegistration.NameOf("console")).Should().BeFalse();

            var valid = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, valid);
            valid.GlobalObject.HasOwnProperty(WebApiRegistration.NameOf("console")).Should().BeTrue();
        }
    }

    [Test]
    public void InstallationIsIdempotentNonClobberingAndDoesNotReadAHostLazyGlobal()
    {
        var (engine, host) = Create(WebApiFeatures.Console | WebApiFeatures.Events | WebApiFeatures.GlobalEvents);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            var reads = 0;
            var hostDescriptor = new LazyPropertyDescriptor<int>(0, _ =>
            {
                reads++;
                return new JsString("host");
            }, PropertyFlag.ConfigurableEnumerableWritable);
            other.GlobalObject.SetProperty("console", hostDescriptor);

            WebApiRegistration.InstallInRealm(engine, other);
            var eventDescriptor = other.GlobalObject.GetOwnProperty("Event");
            WebApiRegistration.InstallInRealm(engine, other);

            reads.Should().Be(0);
            other.GlobalObject.GetOwnProperty("console").Should().BeSameAs(hostDescriptor);
            other.GlobalObject.GetOwnProperty("Event").Should().BeSameAs(eventDescriptor);
            engine._secondaryWebApiRealms!.Snapshot().Should().ContainSingle().Which.Should().BeSameAs(other);
        }
    }

    [Test]
    public void ConcurrentInstallationIsRefusedWithoutMutatingTheGlobal()
    {
        var (engine, host) = Create(WebApiFeatures.Console);
        using (engine)
        using (var entered = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        {
            var other = host.CreateAdditionalRealm();
            engine.SetValue("hold", new Action(() =>
            {
                entered.Set();
                release.Wait();
            }));

            var running = Task.Run(() => engine.Evaluate("hold()"));
            entered.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("the engine-owning callback must start");
            try
            {
                Assert.Throws<InvalidOperationException>(() => WebApiRegistration.InstallInRealm(engine, other));
                other.GlobalObject.HasOwnProperty(WebApiRegistration.NameOf("console")).Should().BeFalse();
            }
            finally
            {
                release.Set();
                running.GetAwaiter().GetResult();
            }
        }
    }

    [Test]
    public void LiveEnableUpdatesEveryInstalledRealmIncludingOneInstalledBeforeAnyFeature()
    {
        var host = new RealmHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        var other = host.CreateAdditionalRealm();
        WebApiRegistration.InstallInRealm(engine, other);

        In(engine, other, "typeof TextEncoder").AsString().Should().Be("undefined");
        engine.WebApi.Enable(WebApiFeatures.Encoding).Should().Be(WebApiFeatures.Encoding);

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("function");
        In(engine, other, "typeof TextEncoder").AsString().Should().Be("function");
        In(engine, other, "new TextEncoder() instanceof TextEncoder").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RestoreDropsEveryRealmListenerListButKeepsTheInstallation()
    {
        var (engine, host) = Create(WebApiFeatures.Events | WebApiFeatures.GlobalEvents);
        using (engine)
        {
            var other = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, other);
            var snapshot = engine.Advanced.CaptureGlobalSnapshot();
            In(engine, other, "var calls = 0; addEventListener('ping', () => calls++);");

            engine.Advanced.RestoreGlobalSnapshot(snapshot);
            In(engine, other, "dispatchEvent(new Event('ping'))");

            In(engine, other, "calls").AsNumber().Should().Be(0);
            In(engine, other, "self === globalThis").AsBoolean().Should().BeTrue();
        }
    }

    [Test]
    public void InstallationDoesNotRetainDeadRealms()
    {
        var (engine, host) = Create(WebApiFeatures.Events | WebApiFeatures.GlobalEvents);
        using (engine)
        {
            const int count = 8;
            var references = new List<WeakReference>(count);
            for (var i = 0; i < count; i++)
            {
                references.Add(InstallAndForget(engine, host));
            }

            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);

            references.Count(static reference => reference.IsAlive).Should().Be(0,
                "the weak installation registry must not extend a realm's lifetime");
            engine._secondaryWebApiRealms!.Snapshot().Should().BeEmpty();
            GC.KeepAlive(host);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference InstallAndForget(Engine engine, RealmHost host)
        {
            var realm = host.CreateAdditionalRealm();
            WebApiRegistration.InstallInRealm(engine, realm);
            return new WeakReference(realm);
        }
    }

    [Test]
    public void DisposeClearsInstallationStateEvenBeforeAnyFeatureIsEnabled()
    {
        var host = new RealmHost();
        var engine = new Engine(options => options.UseHostFactory(_ => host));
        var other = host.CreateAdditionalRealm();
        WebApiRegistration.InstallInRealm(engine, other);
        engine._secondaryWebApiRealms!.Snapshot().Should().ContainSingle();

        engine.Dispose();

        engine._secondaryWebApiRealms.Snapshot().Should().BeEmpty();
    }

    private static (Engine Engine, RealmHost Host) Create(
        WebApiFeatures features,
        DiagnosticsSink? sink = null,
        TimeProvider? timeProvider = null)
    {
        var host = new RealmHost();
        var engine = new Engine(options =>
        {
            options.UseHostFactory(_ => host);
            options.UseWebApis(features);
            options.WebApi.Diagnostics.Sink = sink;
            if (timeProvider is not null)
            {
                options.WebApi.Timers.TimeProvider = timeProvider;
            }
        });
        return (engine, host);
    }

    private static JsValue In(Engine engine, Realm realm, string source)
        => engine.Advanced.WithRealm(realm, scoped => scoped.Evaluate(source));

    private sealed class RealmHost : Host
    {
        public Realm CreateAdditionalRealm() => base.CreateRealm();
    }

    private sealed class RecordingSink : DiagnosticsSink
    {
        internal List<DiagnosticEvent> Reports { get; } = [];

        public override void Report(DiagnosticEvent report) => Reports.Add(report);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }
}
#endif
