// Jint.AotExample is Jint's Native AOT probe. It is published with PublishAot=true and *run* by CI
// (the "aot" job in .github/workflows/pr.yml and build.yml), because that is the only place a Native
// AOT failure actually shows up: <IsAotCompatible> is a property, not evidence, and Jint's own csproj
// still suppresses the eight IL warning codes that would substantiate it.
//
// Three kinds of entry live below.
//
//   Probe(...)        must succeed on every runtime. A failure fails the run.
//   KnownAotGap(...)  must succeed under a JIT and must throw NotSupportedException under Native AOT.
//                     Both directions are checked, so a gap that closes fails the native run and
//                     forces this file to be updated - the same discipline the vendored
//                     web-platform-tests exclusion table uses.
//   KnownTrimmedAway(...)
//                     must read its value under a JIT and must read `undefined` under Native AOT,
//                     because the member was trimmed and nothing reported it. Checked in both
//                     directions for the same reason, and it is the only entry here whose native
//                     expectation is a WRONG ANSWER rather than a failure - which is precisely the
//                     failure mode docs/v5-migration.md section 6.4 says to plan for, because no
//                     diagnostic anywhere reports it.
//
// Every known gap is one shape: a generic instantiation over a VALUE TYPE built at run time, either
// Type.MakeGenericType + Activator.CreateInstance or MethodInfo.MakeGenericMethod. Reference-type
// arguments share canonical code and work; value types need a specific instantiation the compiler
// never saw. The sibling probe beside each gap pins that boundary rather than merely asserting it.
//
// https://github.com/sebastienros/jint/issues/3299 is the enumeration of those sites. Eight exist, not
// the five it was filed with - the T[] wrapper branch arrived later, and two more were never in the
// list - and each is now either a Probe or a KnownAotGap below. The split is whether there is a
// non-generic answer to degrade TO:
//
//   degrades, and the Probe checks the degradation is CORRECT and not merely quiet
//     ArrayWrapperFactory<int> / GenericListWrapperFactory<int> -> ListWrapper
//     GenericListWrapperFactory<int>, target is not an IList    -> plain ObjectWrapper, and
//                                                                  Array.prototype over the
//                                                                  IndexWrappedOperations lane (#3362)
//     ReadOnlyListWrapperFactory<double>                        -> plain ObjectWrapper
//     EnumerableSnapshotFactory<int>                            -> object snapshot
//   throws, because nothing non-generic satisfies the contract asked for
//     Task.FromResult<double>              - nothing else produces a Task<double>
//     hostMethod.MakeGenericMethod(double) - declining reports "no matching overload" for a method
//                                            that plainly exists
//     List<long> for an IEnumerable<long> parameter, and its Collection<short> sibling
//     a closed generic type the SCRIPT named, constructed through importNamespace
//
// Each of the four that throw can be closed by the embedder rather than by Jint, by making the
// instantiation reachable from their own code; docs/v5-migration.md section 6.2 says how. Jint cannot
// do it for them: no signature anywhere in Jint predicts which value types a host's members and a
// script's arguments will produce, and rooting a guessed set would cost every AOT consumer binary size
// for instantiations they never use.

using Jint;
using Jint.Native;
using Jint.Runtime.Interop;
using UnrootedAotProbe;

var failures = 0;
var probes = 0;
var gaps = 0;
var trimmed = 0;
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

// Binds to SetValue<T>(string, T[]), which infers T = int rather than T = int[]. That is what keeps
// this registration free of the four IL3050 an embedder used to pay here: annotating an ARRAY type
// preserves all public methods of System.Array, Array.CreateInstance among them, which is
// [RequiresDynamicCode] and has nothing to do with reading an element.
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

// The reference-type sibling of the row below. IndexedItems<T> is an IList<T> that is deliberately not
// a non-generic IList, and GenericListWrapperFactory<string> shares canonical code, so this host gets
// the typed wrapper on every runtime and its length is writable. That is what makes the row below a
// boundary rather than an assertion about one type.
Probe("IList<string> without IList (GenericListWrapperFactory<string>)", static () =>
{
    var engine = new Engine(static cfg => { cfg.AllowClr(); cfg.Interop.AllowWrite = true; });
    engine.SetValue("host", new IndexedItems<string>("a", "b"));

    if (engine.Evaluate("host").GetType() == typeof(ObjectWrapper))
    {
        throw new InvalidOperationException("expected the typed GenericListWrapper, got the degraded plain ObjectWrapper");
    }

    Expect(2, engine.Evaluate("host.length"));
    Expect("b", engine.Evaluate("host[1]"));
    Expect("a-b", engine.Evaluate("Array.prototype.join.call(host, '-')"));
    Expect("spliced 0, length 2", engine.Evaluate("(function () { try { var r = Array.prototype.splice.call(host, 0, 0); return 'spliced ' + r.length + ', length ' + host.length; } catch (e) { return e.constructor.name; } })()"));
});

// Same site, value-type argument, and the shape that reaches ArrayOperations.IndexWrappedOperations: a
// wrapped CLR collection with an index to read but no IList to read it through. Under a JIT *this host*
// gets the typed GenericListWrapper<int> and the lane is never entered, so Native AOT is what runs the
// lane for this type - but not the only thing that runs the lane, which is what #3362 got wrong and #3394
// corrected: exposing any host collection as IList<T> reaches it under a plain JIT, because
// ResolveArrayLikeWrapperFactoryType scans the exposed type's interfaces and an interface is not among
// its own. Jint.Tests.PublicInterface/HostExposedCollectionTypeTests.cs covers that route on every leg;
// what this probe adds is the value-type degradation, which only a published native binary produces.
// Two bugs sat on the lane undetected until #3302 was fixed beside them: the reflection fallback passed
// the WRAPPER to PropertyInfo.GetValue instead of the CLR target, a TargetException waiting for its first
// read, and SetLength threw a bare NotSupportedException where the wrapper's read-only "length" owes
// script a TypeError. Both fail this probe.
//
// Two facts about the host type are load-bearing, and neither is decoration. The non-generic ICollection
// is where ArrayOperations reads the count, so an IList<T> without it falls through to ObjectOperations.
// And the lane needs the PropertyInfo for IList<int>.Item, which the type descriptor finds by reflecting
// over an interface it obtained at run time - metadata ILC trims by default, so without the
// [DynamicDependency] on IndexedItems the degraded target reads through ObjectOperations here too and
// this probe pins nothing. That is measured, not assumed: typeof(IList<int>).GetProperties() returned an
// empty array in this published binary before that root was added.
Probe("IList<int> without IList (IndexWrappedOperations lane under AOT)", () =>
{
    var engine = new Engine(static cfg => { cfg.AllowClr(); cfg.Interop.AllowWrite = true; });
    engine.SetValue("host", new IndexedItems<int>(1, 2, 3));

    // Which wrapper was built is what decides which lane Array.prototype takes, so assert it instead of
    // hoping: the degraded plain ObjectWrapper is the one routed through IndexWrappedOperations, and a
    // probe that quietly took the typed lane would pin nothing.
    var wrapper = engine.Evaluate("host").GetType();
    if ((wrapper == typeof(ObjectWrapper)) == dynamicCode)
    {
        throw new InvalidOperationException(
            $"expected {(dynamicCode ? "the typed GenericListWrapper" : "the degraded plain ObjectWrapper")} but got {wrapper.Name}");
    }

    // The lane's other precondition, and the one that is invisible from script: without the PropertyInfo
    // for IList<int>.Item the type descriptor reports no integer index, ArrayOperations picks
    // ObjectOperations instead, and every assertion below still passes while testing a different lane.
    if (typeof(IList<int>).GetProperty("Item") is null)
    {
        throw new InvalidOperationException("IList<int>.Item was trimmed, so the IndexWrappedOperations lane is unreachable and this probe would pin nothing");
    }

    // The reads. join, indexOf and filter are Array.prototype generics, so under AOT every element comes
    // back through IndexWrappedOperations.ReadValue - PropertyInfo.GetValue over IList<int>.Item, with
    // the CLR collection as the receiver. host[1] is the contrast: it resolves the type's own indexer
    // and never used that lane, which is exactly why nothing ever caught the receiver bug.
    Expect(3, engine.Evaluate("host.length"));
    Expect(2, engine.Evaluate("host[1]"));
    Expect("1-2-3", engine.Evaluate("Array.prototype.join.call(host, '-')"));
    Expect(2, engine.Evaluate("Array.prototype.indexOf.call(host, 3)"));
    Expect("2-3", engine.Evaluate("Array.prototype.filter.call(host, function (x) { return x > 1; }).join('-')"));

    // The write. splice(0, 0) is the shortest generic that reaches SetLength and nothing else: a
    // deleteCount of 0 with no items performs no element write and no delete, just
    // Set(O, "length", len, true). The degraded wrapper has no IList to resize, so that write meets the
    // wrapper's read-only "length" and becomes a TypeError script can catch - where the code this
    // replaced threw a bare NotSupportedException out of Evaluate, which no script or host catch can
    // see. The typed wrapper resizes instead, so the two runtimes answer differently on purpose.
    Expect(dynamicCode ? "spliced 0, length 3" : "TypeError", engine.Evaluate("(function () { try { var r = Array.prototype.splice.call(host, 0, 0); return 'spliced ' + r.length + ', length ' + host.length; } catch (e) { return e.constructor.name; } })()"));
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

// The other half of the same overload: T = Company, so [DynamicallyAccessedMembers] preserves the
// members script actually reads. Inferring T = Company[] preserved System.Array's members instead and
// left companies[0].name to be trimmed away - a wrong answer with no diagnostic, the same failure mode
// the extension-method probe below exists for.
Probe("Company[] crossing, reading a member of an element", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("companies", new[] { new Company() });
    Expect("Jint", engine.Evaluate("companies[0].name"));
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

Probe("string[] live view (ArrayWrapperFactory<string>)", static () =>
{
    var engine = new Engine(static cfg => { cfg.AllowClr(); cfg.Interop.ArrayConversion = ArrayConversionMode.LiveView; cfg.Interop.AllowWrite = true; });
    engine.SetValue("arr", new ArrayHolder());
    Expect("a", engine.Evaluate("arr.strings[0]"));
    Expect("z", engine.Evaluate("arr.strings[1] = 'z'; arr.strings[1]"));
    Expect("TypeError", engine.Evaluate("try { arr.strings.push('z'); 'no throw' } catch (e) { e.constructor.name }"));
});

// ObjectWrapper.ResolveArrayLikeWrapperFactoryType, T[] branch: ArrayWrapperFactory<int> has no native
// code, so the array degrades to the untyped ListWrapper. The degradation is only worth having if it
// still answers the way the typed wrapper does, which is why this probe writes an element and attempts
// a resize rather than only reading: ListWrapper served the write as a boxed double (InvalidCastException
// into an int[]) and served the resize through IList.Add, which on an array raises the CLR's own
// "Collection was of a fixed size" past script instead of the TypeError the JIT path gives (#3299).
Probe("int[] live view (ListWrapper fallback under AOT)", static () =>
{
    var engine = new Engine(static cfg => { cfg.AllowClr(); cfg.Interop.ArrayConversion = ArrayConversionMode.LiveView; cfg.Interop.AllowWrite = true; });
    engine.SetValue("arr", new ArrayHolder());
    Expect(1, engine.Evaluate("arr.numbers[0]"));
    Expect(9, engine.Evaluate("arr.numbers[1] = 9; arr.numbers[1]"));
    Expect("TypeError", engine.Evaluate("try { arr.numbers.push(9); 'no throw' } catch (e) { e.constructor.name }"));
});

Probe("JS array -> IEnumerable<string> parameter", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("taker", new ListTaker());
    Expect("a,b", engine.Evaluate("taker.joinStrings(['a', 'b'])"));
});

// The parameter type is List<int> itself, so ILC compiled that instantiation for the signature and
// MakeGenericType finds it. This is the boundary worth pinning beside the gap below: whether the site
// works depends on whether the host's own signatures happen to name the closed type Jint asks for.
Probe("JS array -> List<int> parameter", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("taker", new ListTaker());
    Expect(6, engine.Evaluate("taker.sumList([1, 2, 3])"));
});

// DefaultTypeConverter.TryConvertInternal: typeof(List<>).MakeGenericType(long). The parameter is an
// interface, so nothing in the closed program names List<long> and there is no non-generic value that
// satisfies IEnumerable<long> to degrade to - an array would need long[] to exist for the same reason,
// and would hand a host that calls Add a fixed-size collection.
KnownAotGap("JS array -> IEnumerable<long> parameter", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("taker", new ListTaker());
    Expect(6, engine.Evaluate("taker.sumEnumerable([1, 2, 3])"));
});

// The Collection<T> branch of the same method, which builds the inner List<short> the constructor takes.
KnownAotGap("JS array -> Collection<short> parameter", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("taker", new ListTaker());
    Expect(2, engine.Evaluate("taker.countCollection([1, 2])"));
});

Probe("script-built generic CLR type, reference type argument", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr(typeof(Company).Assembly, typeof(string).Assembly));
    Expect("a", engine.Evaluate("var B = importNamespace('AotProbe').Box(System.String); new B('a').read()"));
});

// NamespaceReference.MakeGenericType, driven from script: the type arguments are named by the script, so
// no signature anywhere in the closed program predicts them. The instantiation itself survives - it is
// metadata, and Box<T> is rooted - and the failure lands where the code is needed, on construction.
KnownAotGap("script-built generic CLR type, value type argument", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr(typeof(Company).Assembly, typeof(string).Assembly));
    Expect(3, engine.Evaluate("var B = importNamespace('AotProbe').Box(System.Int16); new B(3).read()"));
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
    // diagnostics for a one-line registration. The array half of this had the same cause and was
    // fixable with an overload; this half is not, because a `where T : Delegate` overload would have
    // the same signature as SetValue<T> after substitution and make every delegate call site
    // ambiguous. See AGENTS.md beside this file.
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

// A nested type is a member a script can name through a TypeReference - the lane TypeResolver ends in
// with Type.GetNestedType - and nothing pinned that it works natively. System.Environment rather than
// one of this project's own types, because those are rooted wholesale by TrimmerRootAssembly.
//
// It passes with no [DynamicallyAccessedMembers(... PublicNestedTypes ...)] anywhere, and that is the
// finding rather than an accident: measured here with the annotation added and removed, SpecialFolder,
// SpecialFolderOption, TimeZoneInfo.TransitionTime/AdjustmentRule, List<int>.Enumerator and
// Dictionary<,>.KeyCollection all resolve either way, so ILC keeps the nested types of a type whose
// metadata it emits. Adding the annotation to SetValue(string, Type) is what a host would PAY for:
// it marks each nested type All, and every nested enum then raises IL3050 in the host's own build
// through the inherited [RequiresDynamicCode] Enum.GetValues(Type) - two of them for the one
// registration below. So Jint states that requirement on TryFindMemberAccessor, where it is read, and
// not at the boundary. Do not "fix" the two IL2072 on TypeReference.ReferenceType by moving it there.
Probe("TypeReference: a nested type of an unrooted framework type", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("Env", typeof(Environment));
    Expect(true, engine.Evaluate("Env.SpecialFolder !== undefined"));
    Expect((int) Environment.SpecialFolder.MyDocuments, engine.Evaluate("Env.SpecialFolder.MyDocuments"));
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

// The annotation group. Everything above measures what the ENGINE does natively; these four measure what
// an ATTRIBUTE preserves, which until now nothing did - Jint.AotExample roots itself and Jint, so every
// host type declared at the bottom of this file survives whatever SetValue<T>'s
// [DynamicallyAccessedMembers] says and a probe over one cannot tell the annotation from the root
// (sebastienros/jint#3479). Their subjects live in Jint.AotExample.UnrootedHost, which the csproj
// deliberately does not root, so the attribute is the only thing between these members and the trimmer.
//
// THE MEASUREMENT IS THAT REMOVING THE ANNOTATION MAKES A PROBE FAIL, and it was run on a published
// native binary rather than reasoned about. Deleting [DynamicallyAccessedMembers] from
// Engine.SetValue<T>(string, T?) and GlobalValueRegistration.RegisterTyped<T> - nothing else - turns four
// probes red:
//
//   unrooted host type through SetValue<T>          expected <preserved> but got <>
//   unrooted IReadOnlyList<string> through SetValue<T>  expected <2> but got <0>
//   Dictionary<string, object> crossing             expected <1> but got <>
//   Dictionary<string, int> crossing                expected <1> but got <>
//
// The last two are the correction #3479 needs: two probes that were ALREADY annotation-sensitive, because
// their subject is a framework type this project does not root either. They discriminate by accident
// rather than by design - nothing said they were the annotation's evidence, and rooting one more assembly
// would have silently retired them - which is why the group below exists as well.
//
// A probe here that passes either way is not testing the annotation and must be deleted rather than kept:
// it reads as evidence and is not.
Probe("unrooted host type through SetValue<T>: the annotation preserves its members", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());

    // T is inferred as PreservedByAnnotation, so the type parameter's [DynamicallyAccessedMembers] names
    // exactly this type and the trimmer keeps its public property, field and method. This is the claim
    // docs/v5-migration.md section 6.3 makes when it points an embedder away from SetValue(string, object?).
    engine.SetValue("h", new PreservedByAnnotation());

    Expect("preserved", engine.Evaluate("h.name"));
    Expect("preserved field", engine.Evaluate("h.field"));
    Expect("hello Mary", engine.Evaluate("h.greet('Mary')"));
});

// The other direction, and the one that had no executable form at all: the SILENT WRONG ANSWER that
// section 6.4 warns about. Nothing preserves the member, nothing reports its absence, and script reads
// `undefined` for a property that is plainly declared on the object it was handed.
KnownTrimmedAway("unrooted host type through SetValue(string, object?): the member is gone", "not preserved", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());

    // The cast is what picks the overload. `object` at the call site is the whole hazard the annotation
    // cannot cover, which is why this overload carries [RequiresUnreferencedCode] instead - IL2026 here,
    // suppressed for the file by the csproj.
    engine.SetValue("h", (object) new TrimmedWithoutAnnotation());

    return engine.Evaluate("h.name");
});

// Array-likeness, which is a second lane and not a second spelling of the probe above: seq[1] resolves the
// type's own indexer, seq.length and the Array.prototype generics arrive only when
// ObjectWrapper.ResolveArrayLikeWrapperFactoryType finds IReadOnlyList<> among the type's interfaces AND
// can read Count. Removing the annotation answers <0> rather than throwing, which is why the length is
// asserted and not merely the element.
//
// It is deliberately NOT called a probe of the Interfaces entry #3396 added to the shared set, because it
// measured as not being one: removing DynamicallyAccessedMemberTypes.Interfaces from
// InteropHelper.DefaultDynamicallyAccessedMemberTypes on its own leaves every assertion here passing. ILC
// keeps the interface implementations of a type whose MethodTable it emits when the interface is used
// anywhere in the closed program, and IReadOnlyList<string> is used by Jint and by the rooted types beside
// this file. What fails here is Count going with PublicProperties. That entry earns its keep in the
// ANALYSIS rather than in the binary - removing it takes the published inventory from 67 to 75 - so do not
// read "no probe discriminates it" as "delete it".
Probe("unrooted IReadOnlyList<string> through SetValue<T>: the annotation preserves what makes it array-like", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("seq", new PreservedInterfaceSequence());

    Expect(2, engine.Evaluate("seq.length"));
    Expect("y", engine.Evaluate("seq[1]"));
    Expect("x-y", engine.Evaluate("Array.prototype.join.call(seq, '-')"));
});

// The edge of the annotation, and the answer to the other half of #3479's question. TypeResolver walks
// type.GetInterfaces() and then iface.GetProperties() to find a member a class implements only through an
// interface. DynamicallyAccessedMemberTypes.Interfaces asks for the implemented interfaces; it does NOT ask
// for their members, so the interface arrives with no property metadata and the walk finds nothing. The
// annotation is present and correct, and the member still reads `undefined`.
//
// Measured, and the observer effect is worth recording because it is what identifies the missing thing:
// adding `typeof(IHiddenBehindAnInterface).GetProperties()` anywhere in this file makes the read succeed,
// because that constant token IS a request to preserve those members. An embedder hitting this closes it
// the way section 6.4 says - by rooting their own assembly - not by widening Jint's annotation.
KnownTrimmedAway("unrooted explicit interface implementation through SetValue<T>: Interfaces does not reach the member", "behind an interface", static () =>
{
    var engine = new Engine(static cfg => cfg.AllowClr());
    engine.SetValue("h", new ExplicitInterfaceOnly());
    return engine.Evaluate("h.hidden");
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
    Console.WriteLine($"ALL PROBES PASSED ({probes} probes, {gaps} known AOT gaps, {trimmed} known trimming gap{(trimmed == 1 ? "" : "s")})");
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

// The trimming counterpart of KnownAotGap. A member of an UNROOTED host type reached through an entry
// point that carries no [DynamicallyAccessedMembers] is preserved by nothing, so under Native AOT it is
// simply not there and the read answers `undefined`. That is the whole point of the entry: a trimmed member
// raises nothing anywhere, so the only way to hold the claim is to assert the wrong answer and check the
// other direction too - a member that starts surviving fails the native run and forces the annotation
// inventory to be re-derived, exactly as a closing KnownAotGap does.
void KnownTrimmedAway(string name, string underJit, Func<JsValue> read)
{
    trimmed++;

    JsValue value;
    try
    {
        value = read();
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}: {ex.GetType().FullName}: {ex.Message}");
        return;
    }

    if (dynamicCode)
    {
        try
        {
            Expect(underJit, value);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine($"FAIL  {name}: {ex.Message}");
            return;
        }

        Console.WriteLine($"PASS  {name} (known trimming gap, the member is present under a JIT)");
        return;
    }

    if (value.IsUndefined())
    {
        Console.WriteLine($"TRIM  {name}: reads undefined, and nothing anywhere said so.");
        return;
    }

    failures++;
    Console.WriteLine($"FAIL  {name}: the member SURVIVED trimming and read <{value.ToObject()}> - something now preserves it, so re-derive what and either promote this to a Probe or find a member that is genuinely unreachable");
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

public class ArrayHolder
{
    public string[] Strings { get; } = ["a", "b"];
    public int[] Numbers { get; } = [1, 2, 3];
}

public class ListTaker
{
    public string JoinStrings(IEnumerable<string> values) => string.Join(",", values);
    public int SumList(List<int> values) { var sum = 0; foreach (var v in values) { sum += v; } return sum; }
    public long SumEnumerable(IEnumerable<long> values) { long sum = 0; foreach (var v in values) { sum += v; } return sum; }
    public int CountCollection(System.Collections.ObjectModel.Collection<short> values) => values.Count;
}

/// <summary>
/// A host collection that implements <see cref="IList{T}"/> and the non-generic
/// <see cref="System.Collections.ICollection"/> but <em>not</em> the non-generic
/// <see cref="System.Collections.IList"/> - the shape that reaches
/// <c>ArrayOperations.IndexWrappedOperations</c> once the typed wrapper cannot be built. Every part of
/// that sentence is load-bearing: the generic interface is what gives the type an index, the non-generic
/// <see cref="System.Collections.ICollection"/> is where the count is read, and the absence of the
/// non-generic <see cref="System.Collections.IList"/> is what leaves the degraded wrapper with nothing to
/// resize.
/// </summary>
public sealed class IndexedItems<T> : IList<T>, System.Collections.ICollection
{
    private readonly List<T> _items;

    /// <summary>
    /// The lane this type exists to exercise reads its elements through the <c>PropertyInfo</c> for
    /// <c>IList&lt;int&gt;.Item</c>, which Jint obtains by reflecting over an interface it discovered at
    /// run time - so no signature preserves it and ILC trims it. Without this root
    /// <c>typeof(IList&lt;int&gt;).GetProperties()</c> is empty in the published binary, the type
    /// descriptor reports no integer index, and the collection is served by <c>ObjectOperations</c>
    /// instead: the same answers, from a different lane, with no diagnostic anywhere - the failure mode
    /// this project already roots its own assembly to avoid.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(IList<int>))]
    public IndexedItems(params T[] items) => _items = [.. items];

    public T this[int index] { get => _items[index]; set => _items[index] = value; }
    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public void Add(T item) => _items.Add(item);
    public void Clear() => _items.Clear();
    public bool Contains(T item) => _items.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(T item) => _items.IndexOf(item);
    public void Insert(int index, T item) => _items.Insert(index, item);
    public bool Remove(T item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
    bool System.Collections.ICollection.IsSynchronized => false;
    object System.Collections.ICollection.SyncRoot => this;
    void System.Collections.ICollection.CopyTo(Array array, int index) => ((System.Collections.ICollection) _items).CopyTo(array, index);
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

namespace AotProbe
{
    public class Box<T>
    {
        private readonly T _value;
        public Box(T value) => _value = value;
        public T Read() => _value;
    }
}
