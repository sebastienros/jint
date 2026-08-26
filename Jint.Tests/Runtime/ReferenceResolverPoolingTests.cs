using Jint.Native;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// Engine.GetValue is handed ownership of a pooled <see cref="Reference"/> and has to give it back
/// before it returns. The exits a reference resolver takes are no exception: a claimed read that
/// keeps the reference drains the pool by one, and once the pool is empty every further claimed read
/// allocates a fresh instance instead of reusing one. The resolver can observe that directly — it is
/// handed the reference itself, so a healthy pool shows up as the same few instances coming back
/// around, while a draining pool hands out a brand new one every time.
/// </summary>
public class ReferenceResolverPoolingTests
{
    private const int Iterations = 200;

    private sealed class InstanceTrackingResolver : IReferenceResolver
    {
        private readonly List<Reference> _distinct = new List<Reference>();

        /// <summary>How many reads the resolver claimed; guards against a test that never reaches it.</summary>
        public int Claims { get; private set; }

        /// <summary>How many different <see cref="Reference"/> instances the engine handed over.</summary>
        public int DistinctReferences => _distinct.Count;

        public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
        {
            Track(reference);
            value = JsValue.Undefined;
            return true;
        }

        public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
        {
            if (!value.IsNullOrUndefined())
            {
                return false;
            }

            Track(reference);
            value = JsValue.Undefined;
            return true;
        }

        public bool TryGetCallable(Engine engine, object callee, out JsValue value)
        {
            value = JsValue.Undefined;
            return false;
        }

        public bool CheckCoercible(JsValue value) => true;

        private void Track(Reference reference)
        {
            Claims++;
            foreach (var seen in _distinct)
            {
                if (ReferenceEquals(seen, reference))
                {
                    return;
                }
            }

            _distinct.Add(reference);
        }
    }

    [Test]
    public void ClaimedNullishPropertyReadReturnsItsReferenceToThePool()
    {
        var resolver = new InstanceTrackingResolver();
        var engine = new Engine(options => options.SetReferenceResolver(resolver, ReferenceResolverInterests.NullishPropertyBase));

        engine.Execute($"var o = {{}}; for (var i = 0; i < {Iterations}; i++) {{ var x = o.missing.value; }}");

        resolver.Claims.Should().Be(Iterations);
        resolver.DistinctReferences.Should().BeLessThanOrEqualTo(ReferencePool.PoolSize);
    }

    [Test]
    public void ClaimedUnresolvableReadReturnsItsReferenceToThePool()
    {
        var resolver = new InstanceTrackingResolver();
        var engine = new Engine(options => options.SetReferenceResolver(resolver, ReferenceResolverInterests.UnresolvableReference));

        engine.Execute($"for (var i = 0; i < {Iterations}; i++) {{ var x = missingGlobal; }}");

        resolver.Claims.Should().Be(Iterations);
        resolver.DistinctReferences.Should().BeLessThanOrEqualTo(ReferencePool.PoolSize);
    }
}
