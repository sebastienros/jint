#if NET8_0_OR_GREATER
#nullable enable

using Jint;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The Workers surface seen from outside the assembly: what a host has to write, and what an engine that
/// asked for it has — which today is deliberately no script surface at all.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything reachable here is reachable by a third party.
/// That is the whole point for this feature: <see cref="WorkerProvider"/> is the extension point a host
/// implements, and a host cannot fabricate a <see cref="WorkerRequest"/> or a
/// <see cref="WorkerConnection"/> — both have internal constructors, because both are things the engine hands
/// over rather than things a host builds.
/// </para>
/// <para>
/// <see cref="TypeofWorkerIsUndefinedEvenWithTheFlagAndProvider"/> is the pin that keeps this change from
/// being the one that also moves the engine: the constructor, the worker global and the error channels land
/// in their own changes, and until they do a script cannot tell that any of this exists.
/// </para>
/// </remarks>
public class WebApiWorkerTests
{
    /// <summary>
    /// A provider a third party could write: it derives from the public abstract class and overrides all
    /// three members using nothing this project cannot see.
    /// </summary>
    private sealed class RecordingWorkerProvider : WorkerProvider
    {
        public int Requests { get; private set; }

        public int Started { get; private set; }

        public int Ended { get; private set; }

        public WorkerEndReason? LastReason { get; private set; }

        public override Engine? CreateWorkerEngine(WorkerRequest request)
        {
            Requests++;

            // What a real provider reads before deciding, all of it public.
            _ = request.Parent;
            _ = request.Specifier;
            _ = request.ReferencingLocation;
            _ = request.Type;
            _ = request.Name;
            _ = request.Depth;
            _ = request.LiveWorkerCount;
            _ = request.TerminationToken;

            return null;
        }

        public override void OnWorkerStarted(WorkerConnection connection)
        {
            Started++;
            connection.HostState = new object();
        }

        public override void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason)
        {
            Ended++;
            LastReason = reason;
        }
    }

    [Fact]
    public void AHostWorkerProviderIsSubclassableOutsideTheAssembly()
    {
        var provider = new RecordingWorkerProvider();

        provider.Should().BeAssignableTo<WorkerProvider>();
        provider.Requests.Should().Be(0);
        provider.Started.Should().Be(0);
        provider.Ended.Should().Be(0);
        provider.LastReason.Should().BeNull();
    }

    [Fact]
    public void UseWorkersIsReachableAndSetsFlagAndProviderTogether()
    {
        var provider = new RecordingWorkerProvider();
        var options = new Options().UseWebApis().UseWorkers(provider);

        (options.WebApi.Features & WebApiFeatures.Workers).Should().Be(WebApiFeatures.Workers);
        options.WebApi.Workers.Provider.Should().BeSameAs(provider);
    }

    [Fact]
    public void TheWorkerOptionsGroupIsReachableAndCarriesTheDocumentedDefaults()
    {
        var options = new Options();

        options.WebApi.Workers.Provider.Should().BeNull("a worker needs a thread, and Jint never starts one");
        options.WebApi.Workers.MaxWorkers.Should().Be(16);
        options.WebApi.Workers.MaxQueuedMessages.Should().Be(16384);
    }

    [Fact]
    public void TheWorkersFlagIsNotPartOfTheDefaultFeatureSet()
    {
        (WebApiFeatures.Default & WebApiFeatures.Workers).Should().Be(WebApiFeatures.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypeofWorkerIsUndefinedEvenWithTheFlagAndProvider(bool enableWorkers)
    {
        var provider = new RecordingWorkerProvider();
        var engine = new Engine(options =>
        {
            options.UseWebApis();
            if (enableWorkers)
            {
                options.UseWorkers(provider);
            }
        });

        engine.Evaluate("typeof Worker").AsString().Should().Be(
            "undefined",
            "the constructor lands in its own change; nothing about this one is observable to a script");

        engine.Evaluate("typeof WorkerGlobalScope").AsString().Should().Be("undefined");
        engine.Evaluate("typeof DedicatedWorkerGlobalScope").AsString().Should().Be("undefined");
        provider.Requests.Should().Be(0);
    }
}
#endif
