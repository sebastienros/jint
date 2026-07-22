namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the ObjectWrapper member inline cache (the wrapper lane in JintMemberExpression): cached
/// descriptors must stay live (CLR getters/setters run on every use), any property define/redefine
/// on the wrapper must invalidate, distinct wrapper instances must not cross-contaminate one call
/// site, dictionary-backed wrappers must stay dynamic, and read-only CLR property write semantics
/// must stay byte-identical in strict and sloppy mode.
/// </summary>
public class InteropWrapperMemberCacheTests
{
    public class CountingHost
    {
        private int _value;

        public int Reads;
        public List<int> WrittenValues { get; } = new();

        public int value
        {
            get
            {
                Reads++;
                return _value;
            }
            set
            {
                WrittenValues.Add(value);
                _value = value;
            }
        }

        public void SetBackingField(int newValue) => _value = newValue;

        public int add(int a, int b) => a + b;
    }

    public class ReadOnlyHost
    {
        public int Fixed => 42;
    }

    [Fact]
    public void PropertyReadsAndWritesInLoopStayLive()
    {
        var host = new CountingHost();
        var engine = new Engine().SetValue("host", host);

        var sum = engine.Evaluate("""
            var sum = 0;
            for (var i = 0; i < 10; i++) {
                host.value = i;
                sum += host.value;
            }
            sum;
            """).AsNumber();

        sum.Should().Be(45);
        // every JS-side read must have invoked the CLR getter (the cache holds the descriptor, never the value)
        host.Reads.Should().Be(10);
        // every JS-side write must have reached the CLR setter
        host.WrittenValues.Should().Equal(Enumerable.Range(0, 10));
    }

    [Fact]
    public void ClrSideMutationsBetweenIterationsAreVisible()
    {
        var host = new CountingHost();
        var engine = new Engine()
            .SetValue("host", host)
            .SetValue("mutate", new Action<int>(host.SetBackingField));

        var result = engine.Evaluate("""
            var r = [];
            for (var i = 0; i < 5; i++) {
                r.push(host.value);
                mutate((i + 1) * 100);
            }
            r.join(',');
            """).AsString();

        result.Should().Be("0,100,200,300,400");
        host.Reads.Should().Be(5);
    }

    [Fact]
    public void DefinePropertyOnWrapperMidLoopInvalidatesReadCache()
    {
        var engine = new Engine().SetValue("host", new CountingHost());

        var result = engine.Evaluate("""
            Object.defineProperty(host, 'extra', { value: 'first', writable: true, configurable: true });
            var r = [];
            for (var i = 0; i < 6; i++) {
                if (i === 3) {
                    Object.defineProperty(host, 'extra', { get: function () { return 'redefined'; }, configurable: true });
                }
                r.push(host.extra);
            }
            r.join(',');
            """).AsString();

        result.Should().Be("first,first,first,redefined,redefined,redefined");
    }

    [Fact]
    public void DefinePropertyOnWrapperMidLoopInvalidatesMethodCallCache()
    {
        var engine = new Engine().SetValue("host", new CountingHost());

        var result = engine.Evaluate("""
            var r = [];
            for (var i = 0; i < 6; i++) {
                if (i === 3) {
                    Object.defineProperty(host, 'add', { value: function (a, b) { return a * b; } });
                }
                r.push(host.add(2, 3));
            }
            r.join(',');
            """).AsString();

        result.Should().Be("5,5,5,6,6,6");
    }

    [Fact]
    public void AlternatingWrapperInstancesAtOneCallSiteDoNotCrossContaminate()
    {
        var first = new CountingHost();
        var second = new CountingHost();
        first.SetBackingField(1);
        second.SetBackingField(2);

        var engine = new Engine()
            .SetValue("first", first)
            .SetValue("second", second);

        var result = engine.Evaluate("""
            function read(h) { return h.value; }
            function write(h, v) { h.value = v; }
            var r = [];
            for (var i = 0; i < 6; i++) {
                var h = i % 2 === 0 ? first : second;
                r.push(read(h));
                write(h, read(h) + 10);
            }
            r.join(',');
            """).AsString();

        result.Should().Be("1,2,11,12,21,22");
        first.value.Should().Be(31);
        second.value.Should().Be(32);
    }

    [Fact]
    public void AlternatingWrapperAndPlainObjectAtOneCallSiteKeepsCorrectValues()
    {
        var host = new CountingHost();
        host.SetBackingField(7);
        var engine = new Engine().SetValue("host", host);

        var result = engine.Evaluate("""
            function read(o) { return o.value; }
            var plain = { value: 'p' };
            var r = [];
            for (var i = 0; i < 6; i++) {
                r.push(read(i % 2 === 0 ? host : plain));
            }
            r.join(',');
            """).AsString();

        result.Should().Be("7,p,7,p,7,p");
    }

    [Fact]
    public void DictionaryBackedWrapperMembersStayDynamic()
    {
        var dictionary = new Dictionary<string, object>();
        var engine = new Engine()
            .SetValue("dict", dictionary)
            .SetValue("clrSet", new Action(() => dictionary["key"] = "clr"))
            .SetValue("clrRemove", new Action(() => dictionary.Remove("key")));

        var result = engine.Evaluate("""
            var r = [];
            for (var i = 0; i < 5; i++) {
                dict.key = i;
                r.push(dict.key);
            }
            clrSet();
            r.push(dict.key);
            clrRemove();
            r.push(dict.key === undefined ? 'gone' : dict.key);
            r.join(',');
            """).AsString();

        result.Should().Be("0,1,2,3,4,clr,gone");
        dictionary.ContainsKey("key").Should().BeFalse();
    }

    [Fact]
    public void ReadOnlyClrPropertyWriteIsIgnoredInSloppyMode()
    {
        var engine = new Engine().SetValue("host", new ReadOnlyHost());

        var result = engine.Evaluate("""
            var r = [];
            for (var i = 0; i < 5; i++) {
                r.push(host.fixed);
                host.fixed = i;
            }
            r.push(host.fixed);
            r.join(',');
            """).AsString();

        result.Should().Be("42,42,42,42,42,42");
    }

    [Fact]
    public void ReadOnlyClrPropertyWriteThrowsTypeErrorInStrictMode()
    {
        var engine = new Engine().SetValue("host", new ReadOnlyHost());

        var result = engine.Evaluate("""
            var r = [];
            // reads populate the wrapper member cache before the write attempts
            r.push(host.fixed);
            r.push(host.fixed);
            var attempt = function () { 'use strict'; host.fixed = 99; };
            for (var i = 0; i < 3; i++) {
                try {
                    attempt();
                    r.push('no-throw');
                } catch (e) {
                    r.push(e instanceof TypeError);
                }
            }
            r.push(host.fixed);
            r.join(',');
            """).AsString();

        result.Should().Be("42,42,true,true,true,42");
    }

    [Fact]
    public void FastLaneAgreesWithForcedSlowLaneAcrossMutations()
    {
        var host = new CountingHost();
        var engine = new Engine()
            .SetValue("host", host)
            .SetValue("mutate", new Action<int>(host.SetBackingField));

        var result = engine.Evaluate("""
            // fast: literal-name lane (cacheable); slow: computed non-literal name forces the Reference path
            function fast() { return host.value; }
            function slow() { var k = 'val'; return host[k + 'ue']; }
            var r = [];
            for (var i = 0; i < 5; i++) {
                host.value = i * 3;
                r.push(fast() === slow());
                mutate(i * 3 + 1);
                r.push(fast() === slow());
            }
            r.join(',');
            """).AsString();

        result.Should().Be(string.Join(",", Enumerable.Repeat("true", 10)));
    }

    [Fact]
    public void CustomMemberAccessorMembersAreNotCached()
    {
        var calls = 0;
        var engine = new Engine(options =>
        {
            options.Interop.MemberAccessor = (e, target, member) =>
            {
                if (member == "dynamicMember")
                {
                    calls++;
                    return calls;
                }

                return null;
            };
        }).SetValue("host", new CountingHost());

        var result = engine.Evaluate("""
            var r = [];
            for (var i = 0; i < 3; i++) {
                r.push(host.dynamicMember);
            }
            r.join(',');
            """).AsString();

        // the custom accessor must be consulted on every read
        result.Should().Be("1,2,3");
        calls.Should().Be(3);
    }
}
