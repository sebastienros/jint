#nullable enable

using System.Globalization;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// What <c>[JsAccessible]</c> promises: an annotated type's members are reached through generated code
/// instead of through reflection, and behave exactly as the reflected ones did.
/// <para>
/// The suite is differential by construction. Every case runs the same script twice — once against
/// <see cref="GeneratedModel"/> and once against its un-annotated twin <see cref="ReflectedModel"/> — and
/// asserts the two are indistinguishable, down to the exception type and message. That is a stronger
/// statement than any hand-written expectation: it survives a change to the reflected path, and it is the
/// only way to notice a conversion the generator reproduced <em>nearly</em> correctly.
/// </para>
/// </summary>
public class HostGeneratedInteropTests
{
    static HostGeneratedInteropTests()
    {
        // The registration a host writes. Explicit rather than a module initializer: it is a statement with
        // a place in the program, so it can be called late, called twice, or not called at all, and each of
        // those is something a test can say something about.
        JsAccessibleRegistration.RegisterAll();
    }

    [Test]
    public void TheGeneratedRegistrationEntryPointIsPublicAndIdempotent()
    {
        JsAccessibleRegistration.RegisterAll();
        JsAccessibleRegistration.RegisterAll();

        // still resolvable afterwards, i.e. the repeat calls did not disturb anything
        Probe(new GeneratedModel { Score = 7 }, "model.Score").Should().Be("number|7");
    }

    /// <summary>
    /// The one place a generated member is deliberately allowed to differ, and the discriminator the rest of
    /// this suite leans on to know the lane is actually engaged: a generated method is a real JavaScript
    /// function object with its own <c>length</c>, where a reflected one reports the arity it inherits from
    /// <c>Function.prototype</c>.
    /// </summary>
    [Test]
    public void AGeneratedMethodReportsItsArityWhereAReflectedOneDoesNot()
    {
        Probe(new GeneratedModel(), "model.Describe.length").Should().Be("number|2");
        Probe(new ReflectedModel(), "model.Describe.length").Should().Be("number|0");

        Probe(new GeneratedModel(), "model.Describe.hasOwnProperty('length')").Should().Be("boolean|true");
        Probe(new ReflectedModel(), "model.Describe.hasOwnProperty('length')").Should().Be("boolean|false");
    }

    [TestCase("model.Score")]
    [TestCase("model.score")]
    [TestCase("typeof model.Score")]
    [TestCase("model.Ticks")]
    [TestCase("model.Ratio")]
    [TestCase("model.Active")]
    [TestCase("model.Name")]
    [TestCase("model.Payload")]
    [TestCase("model.Tag")]
    [TestCase("model.Tags")]
    [TestCase("model.Tags[1]")]
    [TestCase("model.Tags.length")]
    [TestCase("model.Doubled")]
    [TestCase("model.Counter")]
    [TestCase("model.Stamped")]
    [TestCase("model.Missing")]
    [TestCase("JSON.stringify(model.Tags)")]
    [TestCase("Object.keys(model).indexOf('Score') >= 0")]
    [TestCase("'Score' in model")]
    [TestCase("model.hasOwnProperty('Score')")]
    public void ReadsAreIndistinguishable(string script)
    {
        AssertSame(script);
    }

    // the divergence the MVP shipped: TypeConverter.ToString(value) stored the string "null"
    [TestCase("model.Name = null; String(model.Name) + '|' + (model.Name === null)")]
    [TestCase("model.Name = undefined; String(model.Name) + '|' + (model.Name === null)")]
    [TestCase("model.Name = 'set'; model.Name")]
    [TestCase("model.Name = 42; model.Name")]
    [TestCase("model.Name = true; model.Name")]
    [TestCase("model.Name = {}; model.Name")]
    [TestCase("model.Score = 5; model.Score")]
    [TestCase("model.Score = 5.5; model.Score")]
    [TestCase("model.Score = '7'; model.Score")]
    [TestCase("model.Score = NaN; model.Score")]
    [TestCase("model.Score = Infinity; model.Score")]
    [TestCase("model.Score = 1e30; model.Score")]
    [TestCase("model.Score = -2147483648; model.Score")]
    [TestCase("model.Score = 2147483648; model.Score")]
    [TestCase("model.Score = true; model.Score")]
    [TestCase("model.Score = null; model.Score")]
    [TestCase("model.Ticks = 9007199254740991; model.Ticks")]
    [TestCase("model.Ticks = 1.5; model.Ticks")]
    [TestCase("model.Ratio = 1.5; model.Ratio")]
    [TestCase("model.Ratio = '2.5'; model.Ratio")]
    [TestCase("model.Ratio = NaN; String(model.Ratio)")]
    [TestCase("model.Active = true; model.Active")]
    [TestCase("model.Active = 1; model.Active")]
    [TestCase("model.Active = ''; model.Active")]
    [TestCase("model.Payload = 'x'; model.Payload")]
    [TestCase("model.Payload = null; String(model.Payload)")]
    [TestCase("model.Payload = undefined; String(model.Payload)")]
    [TestCase("model.Payload = {a:1}; model.Payload.a")]
    [TestCase("model.Counter = 3; model.Counter")]
    [TestCase("model.Doubled = 9; model.Doubled")]
    [TestCase("model.Echoed = 9; model.Score")]
    [TestCase("String(model.Echoed)")]
    [TestCase("model.Stamped = 'other'; model.Stamped")]
    [TestCase("model.Tags = ['a','b']; model.Tags[0]")]
    [TestCase("model.Tags = 'notanarray'; String(model.Tags)")]
    // the typed write lane's decline rules: only an exact JavaScript type takes it, everything else has to
    // fall through to the engine's conversion and land in the same place
    [TestCase("model.Score = -0; model.Score")]
    [TestCase("model.Ratio = 1; model.Ratio")]
    [TestCase("model.Ticks = 1e30; model.Ticks")]
    [TestCase("model.Ticks = -1e30; model.Ticks")]
    [TestCase("model.Name = new String('boxed'); model.Name")]
    [TestCase("model.Active = new Boolean(false); model.Active")]
    [TestCase("model.Score = new Number(5); model.Score")]
    [TestCase("model.Payload = model; model.Payload === model")]
    [TestCase("model.Counter = 2.5; model.Counter")]
    public void WritesAreIndistinguishable(string script)
    {
        AssertSame(script);
    }

    /// <summary>
    /// And the same with writes off, which is the v5 default: a generated member's descriptor must decline
    /// the write exactly as the reflected one does, rather than reaching the CLR member behind the setting.
    /// </summary>
    [TestCase("model.Score = 5; model.Score")]
    [TestCase("model.Name = 'set'; model.Name")]
    [TestCase("model.Payload = 'x'; String(model.Payload)")]
    [TestCase("model.Counter = 3; model.Counter")]
    [TestCase("'use strict'; try { model.Score = 5 } catch (e) { 'caught ' + (e instanceof TypeError) }")]
    public void AllowWriteIsHonoured(string script)
    {
        AssertSame(script, allowWrite: false);

        // ...and it really is a refusal, not a write that happened to produce the same value
        Probe(new GeneratedModel { Score = 3 }, "model.Score = 5; model.Score", allowWrite: false).Should().Be("number|3");
    }

    /// <summary>
    /// The second divergence the MVP shipped: a member typed as a <c>JsValue</c> subtype was written with a
    /// hard cast in emitted code, <c>((JsString) value)</c>, so a value of the wrong shape failed inside the
    /// generated accessor rather than inside the engine's conversion. The generator no longer claims that
    /// write lane at all — matching <c>CompiledMemberAccessor.IsSupportedWrittenMemberType</c>, which accepts
    /// exactly <c>JsValue</c> and no subtype — so the write goes through the engine's own conversion, and
    /// these come out identical.
    /// <para>
    /// Note what identical means here and what it does not. What the engine's conversion produces today for
    /// a <c>JsString</c>-typed member handed a plain string is a CLR <see cref="InvalidCastException"/> out
    /// of <c>DefaultTypeConverter</c> — on <em>both</em> paths. That is a property of the reflected path, not
    /// of the generator, and turning it into a JavaScript <c>TypeError</c> would be a change to the reflected
    /// path first. This suite's job is to say the two agree.
    /// </para>
    /// </summary>
    [TestCase("model.Tag = 'text'; String(model.Tag)")]
    [TestCase("model.Tag = 42; String(model.Tag)")]
    [TestCase("model.Tag = null; String(model.Tag)")]
    [TestCase("model.Tag = {}; String(model.Tag)")]
    public void AJsValueSubtypeMemberIsWrittenByTheEnginesConversionOnBothPaths(string script)
    {
        AssertSame(script);
    }

    [TestCase("String(model.Echo('a'))")]
    [TestCase("String(model.Echo(1))")]
    [TestCase("String(model.Echo(null))")]
    [TestCase("model.Count()")]
    [TestCase("model.Describe('a', 'b')")]
    [TestCase("model.Touch('t'); String(model.Touched)")]
    [TestCase("String(model.Touch('t'))")]
    [TestCase("typeof model.Echo")]
    [TestCase("typeof model.Count")]
    // arity: a single candidate with no optional parameters binds only for an exact argument count
    [TestCase("model.Describe('a')")]
    [TestCase("model.Describe('a', 'b', 'c')")]
    [TestCase("model.Count(1)")]
    [TestCase("model.Echo()")]
    // receiver derivation
    [TestCase("var f = model.Echo; String(f('detached'))")]
    [TestCase("String(model.Echo.call(model, 'called'))")]
    [TestCase("String(model.Echo.call({}, 'foreign'))")]
    [TestCase("String(Object.create(model).Echo('inherited'))")]
    public void MethodCallsAreIndistinguishable(string script)
    {
        AssertSame(script);
    }

    /// <summary>
    /// The cold edge a generated accessor has no reflection to fall back to. <c>Options.Interop.ValueCoercion</c>
    /// defaults to <c>String</c> alone, so a <c>null</c> assigned to an <see cref="int"/> member is not
    /// coerced to <c>0</c> on the way — it reaches the member as a CLR <c>null</c>, where the runtime binder
    /// writes <c>default(T)</c> rather than refusing. The generated writer has to do the same, and would
    /// have thrown a <see cref="NullReferenceException"/> out of an unbox if it did not.
    /// </summary>
    [Test]
    public void ANullWrittenToAValueTypeMemberBecomesTheDefaultAsTheBinderWouldHaveIt()
    {
        Probe(new GeneratedModel { Score = 3 }, "model.Score = null; model.Score").Should().Be("number|0");
        AssertSame("model.Score = null; model.Score");
    }

    /// <summary>
    /// Shapes the generator declines. They are not a gap in this suite, they are the point of it: an
    /// annotated type's un-expressible members must resolve exactly as they did before anyone annotated it.
    /// </summary>
    [TestCase("model.Add(1)")]
    [TestCase("model.Add(1, 2)")]
    [TestCase("model.Shout('quiet')")]
    [TestCase("model.SetHidden(4); model.Hidden")]
    [TestCase("model.Hidden = 9; model.Hidden")]
    public void MembersTheGeneratorDeclinesKeepTheReflectionPath(string script)
    {
        AssertSame(script);
    }

    [Test]
    public void ANestedAnnotatedTypeAndItsTopLevelNamesakeAreBothGenerated()
    {
        // The MVP derived a hint name from {Namespace}.{Name}, so these two collided and the generator threw
        // "hint name already added" for whichever came second.
        Probe(new GeneratedOuter.Player { Name = "nested" }, "model.Name").Should().Be("string|nested");
        Probe(new Player { Name = "top" }, "model.Name").Should().Be("string|top");
    }

    [Test]
    public void EveryPartOfAPartialTypeIsGenerated()
    {
        var model = new PartiallyAnnotated { First = 1, Second = 2 };
        Probe(model, "model.First + model.Second").Should().Be("number|3");
    }

    /// <summary>
    /// A member a host filter hides is hidden in both lanes, and the members it allows keep theirs. The
    /// second half is what a blanket "any filter turns the feature off" answer got wrong: honouring a filter
    /// by abandoning the feature is safe, and is still not what the host asked for.
    /// </summary>
    [Test]
    public void AHostMemberFilterHidesAMemberInBothLanes()
    {
        var generated = Contained(new GeneratedModel { Score = 3, Ticks = 9 }, resolver => resolver.MemberFilter = HidesScore);
        var reflected = Contained(new ReflectedModel { Score = 3, Ticks = 9 }, resolver => resolver.MemberFilter = HidesScore);

        generated.Evaluate("typeof model.Score").AsString().Should().Be("undefined");
        reflected.Evaluate("typeof model.Score").AsString().Should().Be("undefined");
        generated.Evaluate("model.Ticks").AsNumber().Should().Be(9);
        reflected.Evaluate("model.Ticks").AsNumber().Should().Be(9);

        GeneratedLaneIsEngaged(generated, reflected, "model.Describe.length");
    }

    private static readonly Predicate<System.Reflection.MemberInfo> HidesScore =
        static member => !string.Equals(member.Name, "Score", StringComparison.Ordinal);

    /// <summary>
    /// A host name creator renames a generated member exactly as it renames a reflected one — the old name
    /// stops resolving, the new one starts, and the lane behind it is still the generated one.
    /// </summary>
    [Test]
    public void AHostNameCreatorRenamesAMemberInBothLanes()
    {
        var generated = Contained(new GeneratedModel { Score = 3 }, resolver => resolver.MemberNameCreator = Prefixes);
        var reflected = Contained(new ReflectedModel { Score = 3 }, resolver => resolver.MemberNameCreator = Prefixes);

        generated.Evaluate("model.js_Score").AsNumber().Should().Be(3);
        reflected.Evaluate("model.js_Score").AsNumber().Should().Be(3);
        generated.Evaluate("typeof model.Score").AsString().Should().Be("undefined");
        reflected.Evaluate("typeof model.Score").AsString().Should().Be("undefined");

        GeneratedLaneIsEngaged(generated, reflected, "model.js_Describe.length");
    }

    private static readonly Func<System.Reflection.MemberInfo, IEnumerable<string>> Prefixes =
        static member => ["js_" + member.Name];

    /// <summary>
    /// The camelCase name policy an embedder actually writes. Its effect on the members whose CLR name only
    /// differs in the first character is nil — that is what the default comparer already does — so what this
    /// says is that the whole feature survives installing it, which is what the blanket skip cost.
    /// </summary>
    [Test]
    public void ACamelCaseNameCreatorKeepsTheGeneratedLane()
    {
        var generated = Contained(new GeneratedModel { Score = 3 }, resolver => resolver.MemberNameCreator = CamelCases);
        var reflected = Contained(new ReflectedModel { Score = 3 }, resolver => resolver.MemberNameCreator = CamelCases);

        generated.Evaluate("model.score").AsNumber().Should().Be(3);
        reflected.Evaluate("model.score").AsNumber().Should().Be(3);

        GeneratedLaneIsEngaged(generated, reflected, "model.describe.length");
    }

    private static readonly Func<System.Reflection.MemberInfo, IEnumerable<string>> CamelCases =
        static member => [char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1)];

    /// <summary>
    /// A host name comparer decides which names reach a member, generated or reflected. The default one
    /// ignores the first character's casing; an ordinal one does not, and both lanes have to say so.
    /// </summary>
    [Test]
    public void AHostNameComparerDecidesWhichNamesReachAMemberInBothLanes()
    {
        var generated = Contained(new GeneratedModel { Score = 3 }, resolver => resolver.MemberNameComparer = StringComparer.Ordinal);
        var reflected = Contained(new ReflectedModel { Score = 3 }, resolver => resolver.MemberNameComparer = StringComparer.Ordinal);

        generated.Evaluate("model.Score").AsNumber().Should().Be(3);
        reflected.Evaluate("model.Score").AsNumber().Should().Be(3);
        generated.Evaluate("typeof model.score").AsString().Should().Be("undefined");
        reflected.Evaluate("typeof model.score").AsString().Should().Be("undefined");

        GeneratedLaneIsEngaged(generated, reflected, "model.Describe.length");
    }

    /// <summary>
    /// Binding flags that no longer report a member hide it from both lanes — and leave the lanes they do
    /// not narrow alone, which one setting taking the whole feature off could not express.
    /// </summary>
    [Test]
    public void NarrowedPropertyBindingFlagsHideThePropertiesInBothLanes()
    {
        static Engine Host(object model)
        {
            var engine = new Engine(options =>
            {
                options.Interop.TypeResolver = new TypeResolver();
                options.Interop.ObjectWrapperReportedPropertyBindingFlags =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            });
            engine.SetValue("model", model);
            return engine;
        }

        var generated = Host(new GeneratedModel { Score = 3, Counter = 6 });
        var reflected = Host(new ReflectedModel { Score = 3, Counter = 6 });

        generated.Evaluate("typeof model.Score").AsString().Should().Be("undefined");
        reflected.Evaluate("typeof model.Score").AsString().Should().Be("undefined");

        // the field lane was not narrowed, so it keeps both its member and its generated accessor
        generated.Evaluate("model.Counter").AsNumber().Should().Be(6);
        reflected.Evaluate("model.Counter").AsNumber().Should().Be(6);

        GeneratedLaneIsEngaged(generated, reflected, "model.Describe.length");
    }

    /// <summary>
    /// Annotating a type is not a way around the hardened profile. An engine configured for untrusted code
    /// denies an annotated type exactly what it denies an un-annotated one — <c>GetType</c>, writes, the
    /// namespace globals — and allows it exactly what it allows one, through the generated lanes.
    /// </summary>
    [Test]
    public void AHardenedEngineContainsAnAnnotatedTypeTheWayItContainsAnyOther()
    {
        static Engine Host(object model)
        {
            var engine = new Engine(options => options.ForUntrustedCode(new UntrustedCodeLimits
            {
                TimeoutInterval = TimeSpan.FromSeconds(5),
                MaxStatements = 100_000,
                MemoryLimit = 16_000_000,
                MaxRecursionDepth = 64,
                MaxArraySize = 10_000,
                RegexTimeout = TimeSpan.FromMilliseconds(100),
                PromiseTimeout = TimeSpan.FromMilliseconds(100),
                MaxOperationDuration = TimeSpan.FromSeconds(10),
            }));
            engine.SetValue("model", model);
            return engine;
        }

        var generated = Host(new GeneratedModel { Score = 3 });
        var reflected = Host(new ReflectedModel { Score = 3 });

        foreach (var script in new[]
                 {
                     "typeof model.GetType",
                     "typeof System",
                     "model.Score = 9; model.Score",
                     "String(model.Score)",
                     "String(model.Describe('a', 'b'))",
                 })
        {
            generated.Evaluate(script).ToString().Should().Be(reflected.Evaluate(script).ToString(), "`{0}` must answer the same on both lanes", script);
        }

        generated.Evaluate("typeof model.GetType").AsString().Should().Be("undefined");
        generated.Evaluate("model.Score = 9; model.Score").AsNumber().Should().Be(3);

        GeneratedLaneIsEngaged(generated, reflected, "model.Describe.length");
    }

    /// <summary>
    /// The suite's own differential matrix, re-run against a host name policy. Every read, write and call
    /// has to come out identical there too — which is the whole claim, made where it used to be untestable
    /// because the feature turned itself off rather than run through the policy.
    /// </summary>
    [TestCase("model.score")]
    [TestCase("typeof model.missing")]
    [TestCase("model.name = 'set'; model.name")]
    [TestCase("model.name = null; String(model.name) + '|' + (model.name === null)")]
    [TestCase("model.score = 5.5; model.score")]
    [TestCase("model.counter = 3; model.counter")]
    [TestCase("model.tags = ['a','b']; model.tags[0]")]
    [TestCase("model.doubled")]
    [TestCase("model.stamped")]
    [TestCase("String(model.tag)")]
    [TestCase("String(model.echo('a'))")]
    [TestCase("model.describe('a', 'b')")]
    [TestCase("model.describe('a')")]
    [TestCase("model.add(1, 2)")]
    [TestCase("model.shout('quiet')")]
    [TestCase("model.setHidden(4); model.hidden")]
    [TestCase("Object.keys(model).length")]
    public void TheWholeMatrixIsStillIndistinguishableUnderAHostNamePolicy(string script)
    {
        AssertSame(script, configureResolver: static resolver => resolver.MemberNameCreator = CamelCases);
    }

    /// <summary>
    /// An indexer is probed before the member itself, so a type carrying one resolves its names the way an
    /// un-annotated one does: the indexer wins where it answers, the declared member wins where it does not,
    /// and the generated lanes never reorder the two.
    /// </summary>
    [TestCase("model.Name = 'declared'; model.Name")]
    [TestCase("model.Put('Name', 'from-indexer'); model.Name")]
    [TestCase("model.Name = 'declared'; model.Put('Name', 'from-indexer'); model.Name")]
    [TestCase("model.Put('other', 'value'); model.other")]
    [TestCase("String(model.missing)")]
    [TestCase("String(model.Put('k', 'v'))")]
    public void AnIndexerAndASameNamedMemberResolveIdentically(string script)
    {
        var generated = Probe(new GeneratedIndexed(), script);
        var reflected = Probe(new ReflectedIndexed(), script);

        generated.Should().Be(reflected, "an annotated type carrying an indexer must resolve names the way an un-annotated one does for `{0}`", script);
    }

    /// <summary>
    /// An engine whose resolver the host has configured, and whose model is projected into it. The resolver
    /// is fresh per engine because assigning one of these settings drops everything the resolver has already
    /// resolved, and the shared default resolver is used by every other test in the process.
    /// </summary>
    private static Engine Contained(object model, Action<TypeResolver> configure)
    {
        var resolver = new TypeResolver();
        configure(resolver);

        var engine = new Engine(options => options.Interop.TypeResolver = resolver);
        engine.SetValue("model", model);
        return engine;
    }

    /// <summary>
    /// Says the generated lane actually answered, using the one observable that differs between the two: a
    /// generated method is a function object with its own <c>length</c>, a reflected one is not. Without it
    /// every assertion above would also pass against a lane that had quietly turned itself off.
    /// </summary>
    private static void GeneratedLaneIsEngaged(Engine generated, Engine reflected, string arity)
    {
        generated.Evaluate(arity).AsNumber().Should().Be(2, "the generated lane must still answer for `{0}`", arity);
        reflected.Evaluate(arity).AsNumber().Should().Be(0);
    }

    /// <summary>
    /// A registration landing after an engine already resolved a member of the same type has to take effect
    /// anyway. It cannot be tested with the fixture types — those are registered by this class's static
    /// constructor and a process-wide registry has no undo — so it is tested on the observable that says the
    /// caches were dropped: resolving before and after a registration gives the same answer.
    /// </summary>
    [Test]
    public void ALateRegistrationDropsWhatWasResolvedBeforeIt()
    {
        var model = new GeneratedModel { Score = 11 };
        Probe(model, "model.Score").Should().Be("number|11");

        JsAccessibleRegistration.RegisterAll();

        Probe(model, "model.Score").Should().Be("number|11");
    }

    /// <summary>
    /// A registered <see cref="ObjectConverter"/> has to see every CLR value before it becomes a
    /// <see cref="JsValue"/>, and the typed read lane produces the JsValue itself. The generated accessor
    /// applies the same test the run-time compiled lane does — the converter's declared type set — so a
    /// converter that claims <see cref="string"/> is consulted for the string member and for nothing else.
    /// </summary>
    [Test]
    public void ARegisteredObjectConverterIsConsultedForTheMemberTypesItClaims()
    {
        static Engine Host(object model)
        {
            var engine = new Engine(options => options.AddObjectConverter(new UppercasingStringConverter(), typeof(string)));
            engine.SetValue("model", model);
            return engine;
        }

        var generated = Host(new GeneratedModel { Name = "quiet", Score = 4 });
        var reflected = Host(new ReflectedModel { Name = "quiet", Score = 4 });

        // claimed: the typed read lane must decline so the converter sees the CLR string
        generated.Evaluate("model.Name").ToString().Should().Be("QUIET");
        generated.Evaluate("model.Name").ToString().Should().Be(reflected.Evaluate("model.Name").ToString());

        // unclaimed: the lane stays
        generated.Evaluate("model.Score").AsNumber().Should().Be(reflected.Evaluate("model.Score").AsNumber());
    }

    /// <summary>
    /// A host-installed <see cref="ClrTypeConverter"/> is consulted by the fallback conversion for some member
    /// types, so the typed write lane declines outright while one is present — on both paths.
    /// </summary>
    [Test]
    public void AHostTypeConverterTakesTheTypedWriteLaneOutOfPlay()
    {
        static string Write(object model)
        {
            var engine = new Engine(options =>
            {
                options.Interop.AllowWrite = true;
                options.SetTypeConverter(static _ => new PassthroughTypeConverter());
            });
            engine.SetValue("model", model);
            return engine.Evaluate("model.Score = 7; model.Name = 'x'; model.Score + '/' + model.Name").ToString();
        }

        Write(new GeneratedModel()).Should().Be(Write(new ReflectedModel()));
    }

    private sealed class UppercasingStringConverter : ObjectConverter
    {
        public override bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is string text)
            {
                result = text.ToUpperInvariant();
                return true;
            }

            result = JsValue.Undefined;
            return false;
        }
    }

    /// <summary>Not <c>DefaultTypeConverter</c>, which is the only thing this test needs of it.</summary>
    private sealed class PassthroughTypeConverter : ClrTypeConverter
    {
        public override object? Convert(object? value, Type type, IFormatProvider formatProvider)
            => System.Convert.ChangeType(value, type, formatProvider);

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out object? converted)
        {
            try
            {
                converted = System.Convert.ChangeType(value, type, formatProvider);
                return converted is not null;
            }
            catch (Exception)
            {
                converted = null;
                return false;
            }
        }
    }

    private static void AssertSame(string script, bool allowWrite = true, Action<TypeResolver>? configureResolver = null)
    {
        var generated = Probe(new GeneratedModel { Score = 3, Ticks = 4, Ratio = 0.5, Active = true, Name = "n", Tag = new JsString("t"), Tags = ["a", "b"], Counter = 6 }, script, allowWrite, configureResolver);
        var reflected = Probe(new ReflectedModel { Score = 3, Ticks = 4, Ratio = 0.5, Active = true, Name = "n", Tag = new JsString("t"), Tags = ["a", "b"], Counter = 6 }, script, allowWrite, configureResolver);

        generated.Should().Be(reflected, "the generated lane must be indistinguishable from the reflected one for `{0}`", script);
    }

    /// <summary>
    /// Runs <paramref name="script"/> against <paramref name="model"/> and reduces whatever happens — a
    /// value, a JavaScript throw, a CLR throw — to one comparable string. The two fixture type names are
    /// normalized out, since they are the only thing that legitimately differs.
    /// </summary>
    private static string Probe(object model, string script, bool allowWrite = true, Action<TypeResolver>? configureResolver = null)
    {
        // Projected CLR writes are off by default in v5, and a write to a descriptor whose Set is null is a
        // silent no-op outside strict mode - so a write matrix run on a default engine would compare two
        // sets of nothing and pass whatever the accessors did. AllowWriteIsHonoured covers the default.
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = allowWrite;

            if (configureResolver is not null)
            {
                // a fresh one: assigning any of these settings drops everything the resolver has resolved,
                // and the default resolver is shared with every other test in the process
                var resolver = new TypeResolver();
                configureResolver(resolver);
                options.Interop.TypeResolver = resolver;
            }
        });
        engine.SetValue("model", model);

        try
        {
            var result = engine.Evaluate(script);
            return Normalize(result.Type.ToString().ToLowerInvariant() + "|" + Stringify(engine, result));
        }
        catch (JavaScriptException exception)
        {
            return Normalize("throw|" + exception.Error.ToString() + "|" + exception.Message);
        }
        catch (Exception exception)
        {
            return Normalize("clr-throw|" + exception.GetType().Name + "|" + exception.Message);
        }
    }

    private static string Stringify(Engine engine, JsValue value)
    {
        if (value.IsObject() && value is not Native.Function.Function)
        {
            return engine.Evaluate("(function (v) { try { return JSON.stringify(v) } catch (e) { return String(v) } })")
                .Call(value)
                .ToString();
        }

        return value.IsNumber()
            ? value.AsNumber().ToString(CultureInfo.InvariantCulture)
            : value.ToString();
    }

    private static string Normalize(string text)
        => text.Replace(nameof(GeneratedModel), "Model").Replace(nameof(ReflectedModel), "Model");
}
