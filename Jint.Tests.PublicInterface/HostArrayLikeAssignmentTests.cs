#nullable enable

using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>Third-party opt-in and default behavior for legacy-platform-object assignment.</summary>
public sealed class HostArrayLikeAssignmentTests
{
    private class NamedList(Engine engine) : ArrayLikeObject(engine)
    {
        public override uint Length => 1;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            value = index == 0 ? "element" : JsValue.Undefined;
            return index == 0;
        }

        protected override int NameCount => 1;

        protected override string NameAt(int index) => "named";

        protected override bool TryGetNamedValue(string name, out JsValue value)
        {
            value = name == "named" ? "element" : JsValue.Undefined;
            return name == "named";
        }
    }

    private sealed class WebIdlNamedList(Engine engine) : NamedList(engine)
    {
        protected override bool IgnoreNamedPropertiesInSet => true;
    }

    private sealed class WritableWebIdlNamedList(Engine engine) : NamedList(engine)
    {
        public JsValue Written { get; private set; } = JsValue.Undefined;

        protected override bool IgnoreNamedPropertiesInSet => true;

        protected override bool IsNameWritable(string name) => name == "named";

        protected override bool TrySetNamedValue(string name, JsValue value)
        {
            Written = value;
            return true;
        }
    }

    [Test]
    public void OptInKeepsNamedSettersAheadOfOrdinaryDescriptorSelection()
    {
        var engine = new Engine();
        var list = new WritableWebIdlNamedList(engine);
        engine.SetValue("list", list);
        engine.Evaluate("Reflect.set(list, 'named', 'setter')").Should().Be(true);
        list.Written.Should().Be("setter");
        engine.Evaluate("""
            (() => {
              const receiver = Object.create(list);
              return Reflect.set(list, 'named', 'own', receiver) && receiver.named === 'own';
            })()
            """).Should().Be(true);
        list.Written.Should().Be("setter");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OptInControlsWhetherAnInheritingReceiverCanShadowTheProjection(bool optIn)
    {
        var engine = new Engine();
        engine.SetValue("list", optIn ? new WebIdlNamedList(engine) : new NamedList(engine));
        engine.SetValue("optIn", optIn);
        engine.Evaluate("""
            (() => {
              const receiver = Object.create(list);
              if (receiver.named !== 'element') return false;
              if (Reflect.set(list, 'named', 'own', receiver) !== optIn) return false;
              if (Object.hasOwn(receiver, 'named') !== optIn) return false;
              if (receiver.named !== (optIn ? 'own' : 'element')) return false;
              if (Reflect.set(list, 'named', 'replace')) return false;
              if (Reflect.set(list, '0', 'replace', receiver)) return false;
              if (Reflect.set(list, 'length', 2, receiver)) return false;
              return list.named === 'element' && list[0] === 'element' && list.length === 1 &&
                !Object.getOwnPropertyDescriptor(list, 'named').writable;
            })()
            """).Should().Be(true);
    }

    [Test]
    public void OptInStillUsesOrdinaryStoredPropertiesAndReceiverDescriptors()
    {
        var engine = new Engine();
        engine.SetValue("list", new WebIdlNamedList(engine));
        engine.Evaluate("""
            (() => {
              Object.defineProperty(list, 'locked', { value: 1 });
              const receiver = Object.create(list);
              if (Reflect.set(list, 'locked', 2, receiver)) return false;
              Object.defineProperty(receiver, 'named', { value: 'locked' });
              if (Reflect.set(list, 'named', 'replace', receiver)) return false;
              const accessor = Object.create(list);
              let calls = 0;
              Object.defineProperty(accessor, 'named', { set() { calls++; } });
              return !Reflect.set(list, 'named', 'replace', accessor) && calls === 0 &&
                !Reflect.set(list, 'named', 'replace', 1) && receiver.named === 'locked';
            })()
            """).Should().Be(true);
    }

    [Test]
    public void OptInStillCallsAPrototypeSetterWithTheOriginalReceiver()
    {
        var engine = new Engine();
        engine.SetValue("list", new WebIdlNamedList(engine));
        engine.Evaluate("""
            (() => {
              let seenThis, seenValue;
              Object.setPrototypeOf(list, { set named(value) { seenThis = this; seenValue = value; } });
              const receiver = Object.create(list);
              return Reflect.set(list, 'named', 'assigned', receiver) && seenThis === receiver &&
                seenValue === 'assigned' && !Object.hasOwn(receiver, 'named') && list.named === 'element';
            })()
            """).Should().Be(true);
    }
}
