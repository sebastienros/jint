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

        Assert.Equal(45, sum);
        // every JS-side read must have invoked the CLR getter (the cache holds the descriptor, never the value)
        Assert.Equal(10, host.Reads);
        // every JS-side write must have reached the CLR setter
        Assert.Equal(Enumerable.Range(0, 10), host.WrittenValues);
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

        Assert.Equal("0,100,200,300,400", result);
        Assert.Equal(5, host.Reads);
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

        Assert.Equal("first,first,first,redefined,redefined,redefined", result);
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

        Assert.Equal("5,5,5,6,6,6", result);
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

        Assert.Equal("1,2,11,12,21,22", result);
        Assert.Equal(31, first.value);
        Assert.Equal(32, second.value);
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

        Assert.Equal("7,p,7,p,7,p", result);
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

        Assert.Equal("0,1,2,3,4,clr,gone", result);
        Assert.False(dictionary.ContainsKey("key"));
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

        Assert.Equal("42,42,42,42,42,42", result);
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

        Assert.Equal("42,42,true,true,true,42", result);
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

        Assert.Equal(string.Join(",", Enumerable.Repeat("true", 10)), result);
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
        Assert.Equal("1,2,3", result);
        Assert.Equal(3, calls);
    }
}
