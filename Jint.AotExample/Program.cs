// Jint.AotExample is Jint's Native AOT probe. It is published with PublishAot=true and *run* by CI
// (the "aot" job in .github/workflows/pr.yml and build.yml), because that is the only place a Native
// AOT failure actually shows up: <IsAotCompatible> is a property, not evidence, and Jint's own csproj
// still suppresses the eight IL warning codes that would substantiate it.
//
// Two kinds of entry live below.
//
//   Probe(...)        must succeed on every runtime. A failure fails the run.
//   KnownAotGap(...)  must succeed under a JIT and must throw NotSupportedException under Native AOT.
//                     Both directions are checked, so a gap that closes fails the native run and
//                     forces this file to be updated - the same discipline the vendored
//                     web-platform-tests exclusion table uses.
//
// Every known gap is one shape: a generic instantiation over a VALUE TYPE built at run time, either
// Type.MakeGenericType + Activator.CreateInstance or MethodInfo.MakeGenericMethod. Reference-type
// arguments share canonical code and work; value types need a specific instantiation the compiler
// never saw. The sibling probe beside each gap pins that boundary rather than merely asserting it.
// All five were https://github.com/sebastienros/jint/issues/3299; three of them now degrade to an
// untyped wrapper instead of throwing, and their rows below are Probes rather than gaps. The two
// that remain are the two with no non-generic answer to degrade TO: there is no way to produce a
// Task<double> without Task.FromResult<double>, and declining a generic host method would report
// "no matching overload" for a method that plainly exists. Both would trade a diagnosable throw for
// a wrong answer, so both stay gaps.

using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

var failures = 0;
var probes = 0;
var gaps = 0;
// A Native AOT image has no managed assembly file to point at, so Assembly.Location is the empty
// string there and a path under every other host. Neither of the two obvious alternatives works:
// PublishAot=true writes both RuntimeFeature.IsDynamicCodeSupported and .IsDynamicCodeCompiled as
// false into runtimeconfig.json, so `dotnet run` on this very project reports itself as Native AOT;
// and asking the capability directly - typeof(X<>).MakeGenericType(typeof(SomeStruct)) - is folded
// by ILC at compile time, because both tokens are constants. Jint's own sites are not foldable
// precisely because their Type comes from a value crossing the boundary at run time.
// IL3000 says this property "always returns an empty string" once the app is published as a single
// file or Native AOT image - which is the fact being read, not a mistake being made, so the csproj
// suppresses that one code. A source #pragma would not do: it reaches Roslyn but not ILC, which
// re-derives every IL diagnostic when it compiles the closed program.
var dynamicCode = System.Reflection.Assembly.GetExecutingAssembly().Location.Length != 0;

Console.WriteLine(dynamicCode
    ? "runtime: JIT - known AOT gaps are expected to WORK here"
    : "runtime: Native AOT - known AOT gaps are expected to THROW here");
Console.WriteLine();

Probe("member, field, indexer and method access", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("company", new Company());
    Expect("Jint", engine.Evaluate("company.name"));
    Expect("public field value", engine.Evaluate("company.field"));
    Expect(42, engine.Evaluate("company[42]"));
    Expect("Hello Mary!", engine.Evaluate("company.sayHello('Mary')"));
});

Probe("int[] crossing", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("numbers", new[] { 1, 2, 3 });
    Expect(6, engine.Evaluate("numbers[0] + numbers[1] + numbers[2]"));
    Expect(12, engine.Evaluate("numbers.map(function (x) { return x * 2; }).reduce(function (a, b) { return a + b; }, 0)"));
});

Probe("List<string> crossing (GenericListWrapperFactory<string>)", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("list", new List<string> { "a", "b" });
    Expect(2, engine.Evaluate("list.length"));
    Expect("b", engine.Evaluate("list[1]"));
});

// ObjectWrapper.TryBuildArrayLikeWrapper: typeof(GenericListWrapperFactory<>).MakeGenericType(int)
// has no native code under AOT, and the site now degrades to the non-generic ListWrapper rather than
// letting NotSupportedException reach script. This is a Probe, not a KnownAotGap, because the
// degradation has to be correct and not merely quiet - a wrapper answering the wrong length would
// satisfy a try/catch and fail here.
Probe("List<int> crossing (ListWrapper fallback under AOT)", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("list", new List<int> { 1, 2, 3 });
    Expect(3, engine.Evaluate("list.length"));
    Expect(2, engine.Evaluate("list[1]"));
});

Probe("IReadOnlyList<string> crossing (ReadOnlyListWrapperFactory<string>)", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("ro", new ReadOnlyStrings());
    Expect("x", engine.Evaluate("ro[0]"));
});

// Same site as above, IReadOnlyList<> branch. ReadOnlyDoubles is not an IList either, so the
// degradation goes one step further than the row above: no array-like wrapper at all, and the read is
// served by a plain ObjectWrapper resolving the type's own indexer. Array-likeness is what is lost -
// ro.length and the Array.prototype generics go with the typed factory, ro[0] does not.
Probe("IReadOnlyList<double> crossing (plain ObjectWrapper fallback under AOT)", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("ro", new ReadOnlyDoubles());
    Expect(1.5, engine.Evaluate("ro[0]"));
});

Probe("IEnumerable<string> snapshot (EnumerableSnapshotFactory<string>)", static () =>
{
    var engine = new Engine(static cfg =>
    {
        cfg.AllowClr();
        cfg.Interop.EnumerableConversion = EnumerableConversionMode.Snapshot;
    });
    engine.SetValue("seq", Strings());
    Expect(2, engine.Evaluate("seq.length"));
});

// ObjectWrapper.ResolveEnumerableSnapshotFactory: same MakeGenericType + Activator shape, degrading
// to ObjectEnumerableSnapshotFactory, which snapshots the sequence as objects rather than as int.
Probe("IEnumerable<int> snapshot (object snapshot fallback under AOT)", static () =>
{
    var engine = new Engine(static cfg =>
    {
        cfg.AllowClr();
        cfg.Interop.EnumerableConversion = EnumerableConversionMode.Snapshot;
    });
    engine.SetValue("seq", Numbers());
    Expect(3, engine.Evaluate("seq.length"));
});

Probe("Dictionary<string, object> crossing", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("map", new Dictionary<string, object> { ["a"] = 1, ["b"] = "two" });
    Expect(1, engine.Evaluate("map.a"));
    Expect("two", engine.Evaluate("map.b"));
});

Probe("Dictionary<string, int> crossing", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("map", new Dictionary<string, int> { ["a"] = 1 });
    Expect(1, engine.Evaluate("map.a"));
});

Probe("delegate crossing: JS function -> Func<int, int>", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("applier", new Applier());
    Expect(10, engine.Evaluate("applier.apply(function (x) { return x * 2; }, 5)"));
});

Probe("delegate crossing: JS function -> Func<string, string>", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("applier", new Applier());
    Expect("ab", engine.Evaluate("applier.applyString(function (s) { return s + 'b'; }, 'a')"));
});

// DefaultTypeConverter.GetFromResultMethod: Task.FromResult<double> through MakeGenericMethod.
KnownAotGap("delegate crossing: JS function -> Func<double, Task<double>>", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("applier", new Applier());
    Expect(10, engine.Evaluate("applier.applyAsync(function (x) { return x * 2; }, 5)"));
});

Probe("delegate crossing: CLR Func<int, int> -> JS", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());

    // Deliberately cast. An uncast `new Func<int, int>(...)` binds to SetValue<T> rather than
    // SetValue(string, Delegate), and SetValue<T>'s [DynamicallyAccessedMembers] then demands every
    // public method of Func<int, int> - including the inherited, [RequiresUnreferencedCode]
    // Delegate.CreateDelegate overloads. The embedder's own project gets six IL2026/IL2111
    // diagnostics for a one-line registration; see the AOT section of Jint/Runtime/Interop/AGENTS.md.
    engine.SetValue("twice", (Delegate) new Func<int, int>(static x => x * 2));

    Expect(8, engine.Evaluate("twice(4)"));
});

Probe("generic method call, reference type argument", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("g", new GenericHost());
    Expect("hi", engine.Evaluate("g.identity('hi')"));
});

// MethodInfoFunction.ResolveMethod: methodInfo.MakeGenericMethod(double).
KnownAotGap("generic method call, value type argument", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("g", new GenericHost());
    Expect(7, engine.Evaluate("g.identity(7)"));
});

Probe("TypeReference: constructing a CLR type from script", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("Company", typeof(Company));
    Expect("Jint", engine.Evaluate("new Company().name"));
});

// Only passes because the csproj roots this assembly as well as Jint. Without that, the extension
// method's metadata is trimmed and `company.shout()` fails as "not a function" - a wrong answer
// rather than an AOT diagnostic, which is why the probe is here.
Probe("extension methods", static () =>
{
    var engine = new Engine(static cfg =>
    {
        cfg.AllowClr(typeof(Company).Assembly);
        cfg.AddExtensionMethods(typeof(CompanyExtensions));
    });
    engine.SetValue("company", new Company());
    Expect("JINT", engine.Evaluate("company.shout()"));
});

Probe("JSON round trip", static () =>
{
    var engine = new Engine();
    Expect("{\"a\":1,\"b\":[1,2,3]}", engine.Evaluate("JSON.stringify({ a: 1, b: [1, 2, 3] })"));
    Expect(2, engine.Evaluate("JSON.parse('{\"a\":2}').a"));
});

// The hypothesised AOT-safe subset: no interop at all, so none of the reflection above is reachable.
Probe("interop disabled engine", static () =>
{
    var engine = new Engine(static o => o.Interop.Enabled = false);
    Expect(55, engine.Evaluate("(function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); })(10)"));
    Expect("ABC", engine.Evaluate("'abc'.toUpperCase()"));
    Expect(6, engine.Evaluate("[1,2,3].reduce((a, b) => a + b, 0)"));
    Expect(true, engine.Evaluate("/^a+b$/.test('aaab')"));
    Expect("1,2,3", engine.Evaluate("[...new Set([1,2,3])].join(',')"));
});

Probe("promise and async function", static () =>
{
    var engine = new Engine();
    Expect(42, engine.Evaluate("(async function () { return 42; })()").UnwrapIfPromise());
});

Console.WriteLine();
if (failures == 0)
{
    Console.WriteLine($"ALL PROBES PASSED ({probes} probes, {gaps} known AOT gaps)");
    return 0;
}

Console.WriteLine($"{failures} PROBE(S) FAILED");
return 1;

static IEnumerable<int> Numbers()
{
    yield return 1;
    yield return 2;
    yield return 3;
}

static IEnumerable<string> Strings()
{
    yield return "a";
    yield return "b";
}

void Probe(string name, Action action)
{
    probes++;
    try
    {
        action();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}: {ex.GetType().FullName}: {ex.Message}");
    }
}

void KnownAotGap(string name, Action action)
{
    gaps++;
    try
    {
        action();
    }
    catch (NotSupportedException ex) when (!dynamicCode)
    {
        Console.WriteLine($"GAP   {name}: {ex.Message.Split(". ")[0]}.");
        return;
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}: {ex.GetType().FullName}: {ex.Message}");
        return;
    }

    if (dynamicCode)
    {
        Console.WriteLine($"PASS  {name} (known AOT gap, works under a JIT)");
        return;
    }

    failures++;
    Console.WriteLine($"FAIL  {name}: this known AOT gap now WORKS - promote it from KnownAotGap to Probe");
}

static void Expect(object expected, JsValue actual)
{
    var got = actual.ToObject();
    var expectedText = Convert.ToString(expected, System.Globalization.CultureInfo.InvariantCulture);
    var actualText = Convert.ToString(got, System.Globalization.CultureInfo.InvariantCulture);
    if (!string.Equals(expectedText, actualText, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"expected <{expectedText}> but got <{actualText}>");
    }
}

public class Company
{
    public string Field = "public field value";
    public string Name => "Jint";
    public string SayHello(string name) => $"Hello {name}!";
    public int this[int index] => index;
}

public static class CompanyExtensions
{
    public static string Shout(this Company company) => company.Name.ToUpperInvariant();
}

public class Applier
{
    public int Apply(Func<int, int> f, int value) => f(value);
    public string ApplyString(Func<string, string> f, string value) => f(value);
    public double ApplyAsync(Func<double, Task<double>> f, double value) => f(value).GetAwaiter().GetResult();
}

public class GenericHost
{
    public T Identity<T>(T value) => value;
}

public sealed class ReadOnlyStrings : IReadOnlyList<string>
{
    private readonly List<string> _items = ["x", "y"];
    public string this[int index] => _items[index];
    public int Count => _items.Count;
    public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

public sealed class ReadOnlyDoubles : IReadOnlyList<double>
{
    private readonly List<double> _items = [1.5, 2.5];
    public double this[int index] => _items[index];
    public int Count => _items.Count;
    public IEnumerator<double> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
