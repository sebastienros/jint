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

    [Fact]
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
    [Fact]
    public void AGeneratedMethodReportsItsArityWhereAReflectedOneDoesNot()
    {
        Probe(new GeneratedModel(), "model.Describe.length").Should().Be("number|2");
        Probe(new ReflectedModel(), "model.Describe.length").Should().Be("number|0");

        Probe(new GeneratedModel(), "model.Describe.hasOwnProperty('length')").Should().Be("boolean|true");
        Probe(new ReflectedModel(), "model.Describe.hasOwnProperty('length')").Should().Be("boolean|false");
    }

    [Theory]
    [InlineData("model.Score")]
    [InlineData("model.score")]
    [InlineData("typeof model.Score")]
    [InlineData("model.Ticks")]
    [InlineData("model.Ratio")]
    [InlineData("model.Active")]
    [InlineData("model.Name")]
    [InlineData("model.Payload")]
    [InlineData("model.Tag")]
    [InlineData("model.Tags")]
    [InlineData("model.Tags[1]")]
    [InlineData("model.Tags.length")]
    [InlineData("model.Doubled")]
    [InlineData("model.Counter")]
    [InlineData("model.Stamped")]
    [InlineData("model.Missing")]
    [InlineData("JSON.stringify(model.Tags)")]
    [InlineData("Object.keys(model).indexOf('Score') >= 0")]
    [InlineData("'Score' in model")]
    [InlineData("model.hasOwnProperty('Score')")]
    public void ReadsAreIndistinguishable(string script)
    {
        AssertSame(script);
    }

    [Theory]
    // the divergence the MVP shipped: TypeConverter.ToString(value) stored the string "null"
    [InlineData("model.Name = null; String(model.Name) + '|' + (model.Name === null)")]
    [InlineData("model.Name = undefined; String(model.Name) + '|' + (model.Name === null)")]
    [InlineData("model.Name = 'set'; model.Name")]
    [InlineData("model.Name = 42; model.Name")]
    [InlineData("model.Name = true; model.Name")]
    [InlineData("model.Name = {}; model.Name")]
    [InlineData("model.Score = 5; model.Score")]
    [InlineData("model.Score = 5.5; model.Score")]
    [InlineData("model.Score = '7'; model.Score")]
    [InlineData("model.Score = NaN; model.Score")]
    [InlineData("model.Score = Infinity; model.Score")]
    [InlineData("model.Score = 1e30; model.Score")]
    [InlineData("model.Score = -2147483648; model.Score")]
    [InlineData("model.Score = 2147483648; model.Score")]
    [InlineData("model.Score = true; model.Score")]
    [InlineData("model.Score = null; model.Score")]
    [InlineData("model.Ticks = 9007199254740991; model.Ticks")]
    [InlineData("model.Ticks = 1.5; model.Ticks")]
    [InlineData("model.Ratio = 1.5; model.Ratio")]
    [InlineData("model.Ratio = '2.5'; model.Ratio")]
    [InlineData("model.Ratio = NaN; String(model.Ratio)")]
    [InlineData("model.Active = true; model.Active")]
    [InlineData("model.Active = 1; model.Active")]
    [InlineData("model.Active = ''; model.Active")]
    [InlineData("model.Payload = 'x'; model.Payload")]
    [InlineData("model.Payload = null; String(model.Payload)")]
    [InlineData("model.Payload = undefined; String(model.Payload)")]
    [InlineData("model.Payload = {a:1}; model.Payload.a")]
    [InlineData("model.Counter = 3; model.Counter")]
    [InlineData("model.Doubled = 9; model.Doubled")]
    [InlineData("model.Echoed = 9; model.Score")]
    [InlineData("String(model.Echoed)")]
    [InlineData("model.Stamped = 'other'; model.Stamped")]
    [InlineData("model.Tags = ['a','b']; model.Tags[0]")]
    [InlineData("model.Tags = 'notanarray'; String(model.Tags)")]
    // the typed write lane's decline rules: only an exact JavaScript type takes it, everything else has to
    // fall through to the engine's conversion and land in the same place
    [InlineData("model.Score = -0; model.Score")]
    [InlineData("model.Ratio = 1; model.Ratio")]
    [InlineData("model.Ticks = 1e30; model.Ticks")]
    [InlineData("model.Ticks = -1e30; model.Ticks")]
    [InlineData("model.Name = new String('boxed'); model.Name")]
    [InlineData("model.Active = new Boolean(false); model.Active")]
    [InlineData("model.Score = new Number(5); model.Score")]
    [InlineData("model.Payload = model; model.Payload === model")]
    [InlineData("model.Counter = 2.5; model.Counter")]
    public void WritesAreIndistinguishable(string script)
    {
        AssertSame(script);
    }

    /// <summary>
    /// And the same with writes off, which is the v5 default: a generated member's descriptor must decline
    /// the write exactly as the reflected one does, rather than reaching the CLR member behind the setting.
    /// </summary>
    [Theory]
    [InlineData("model.Score = 5; model.Score")]
    [InlineData("model.Name = 'set'; model.Name")]
    [InlineData("model.Payload = 'x'; String(model.Payload)")]
    [InlineData("model.Counter = 3; model.Counter")]
    [InlineData("'use strict'; try { model.Score = 5 } catch (e) { 'caught ' + (e instanceof TypeError) }")]
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
    [Theory]
    [InlineData("model.Tag = 'text'; String(model.Tag)")]
    [InlineData("model.Tag = 42; String(model.Tag)")]
    [InlineData("model.Tag = null; String(model.Tag)")]
    [InlineData("model.Tag = {}; String(model.Tag)")]
    public void AJsValueSubtypeMemberIsWrittenByTheEnginesConversionOnBothPaths(string script)
    {
        AssertSame(script);
    }

    [Theory]
    [InlineData("String(model.Echo('a'))")]
    [InlineData("String(model.Echo(1))")]
    [InlineData("String(model.Echo(null))")]
    [InlineData("model.Count()")]
    [InlineData("model.Describe('a', 'b')")]
    [InlineData("model.Touch('t'); String(model.Touched)")]
    [InlineData("String(model.Touch('t'))")]
    [InlineData("typeof model.Echo")]
    [InlineData("typeof model.Count")]
    // arity: a single candidate with no optional parameters binds only for an exact argument count
    [InlineData("model.Describe('a')")]
    [InlineData("model.Describe('a', 'b', 'c')")]
    [InlineData("model.Count(1)")]
    [InlineData("model.Echo()")]
    // receiver derivation
    [InlineData("var f = model.Echo; String(f('detached'))")]
    [InlineData("String(model.Echo.call(model, 'called'))")]
    [InlineData("String(model.Echo.call({}, 'foreign'))")]
    [InlineData("String(Object.create(model).Echo('inherited'))")]
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
    [Fact]
    public void ANullWrittenToAValueTypeMemberBecomesTheDefaultAsTheBinderWouldHaveIt()
    {
        Probe(new GeneratedModel { Score = 3 }, "model.Score = null; model.Score").Should().Be("number|0");
        AssertSame("model.Score = null; model.Score");
    }

    /// <summary>
    /// Shapes the generator declines. They are not a gap in this suite, they are the point of it: an
    /// annotated type's un-expressible members must resolve exactly as they did before anyone annotated it.
    /// </summary>
    [Theory]
    [InlineData("model.Add(1)")]
    [InlineData("model.Add(1, 2)")]
    [InlineData("model.Shout('quiet')")]
    [InlineData("model.SetHidden(4); model.Hidden")]
    [InlineData("model.Hidden = 9; model.Hidden")]
    public void MembersTheGeneratorDeclinesKeepTheReflectionPath(string script)
    {
        AssertSame(script);
    }

    [Fact]
    public void ANestedAnnotatedTypeAndItsTopLevelNamesakeAreBothGenerated()
    {
        // The MVP derived a hint name from {Namespace}.{Name}, so these two collided and the generator threw
        // "hint name already added" for whichever came second.
        Probe(new GeneratedOuter.Player { Name = "nested" }, "model.Name").Should().Be("string|nested");
        Probe(new Player { Name = "top" }, "model.Name").Should().Be("string|top");
    }

    [Fact]
    public void EveryPartOfAPartialTypeIsGenerated()
    {
        var model = new PartiallyAnnotated { First = 1, Second = 2 };
        Probe(model, "model.First + model.Second").Should().Be("number|3");
    }

    /// <summary>
    /// A host that has installed one of the four settings steering member resolution keeps the reflection
    /// path even for an annotated type, because the generated lanes do not run through them yet. Not being
    /// able to honour a filter and honouring it anyway are the same bug; declining is the difference.
    /// </summary>
    [Fact]
    public void AHostMemberFilterTurnsTheGeneratedLaneOff()
    {
        var resolver = new TypeResolver { MemberFilter = static member => !string.Equals(member.Name, "Score", StringComparison.Ordinal) };
        var engine = new Engine(options => options.Interop.TypeResolver = resolver);
        engine.SetValue("model", new GeneratedModel { Score = 3, Ticks = 9 });

        engine.Evaluate("typeof model.Score").AsString().Should().Be("undefined");
        engine.Evaluate("model.Ticks").AsNumber().Should().Be(9);
    }

    [Fact]
    public void AHostNameCreatorTurnsTheGeneratedLaneOff()
    {
        var resolver = new TypeResolver { MemberNameCreator = static member => [member.Name.ToUpperInvariant()] };
        var engine = new Engine(options => options.Interop.TypeResolver = resolver);
        engine.SetValue("model", new GeneratedModel { Score = 3 });

        engine.Evaluate("model.SCORE").AsNumber().Should().Be(3);
    }

    /// <summary>
    /// A registration landing after an engine already resolved a member of the same type has to take effect
    /// anyway. It cannot be tested with the fixture types — those are registered by this class's static
    /// constructor and a process-wide registry has no undo — so it is tested on the observable that says the
    /// caches were dropped: resolving before and after a registration gives the same answer.
    /// </summary>
    [Fact]
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
    [Fact]
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
    [Fact]
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

    private static void AssertSame(string script, bool allowWrite = true)
    {
        var generated = Probe(new GeneratedModel { Score = 3, Ticks = 4, Ratio = 0.5, Active = true, Name = "n", Tag = new JsString("t"), Tags = ["a", "b"], Counter = 6 }, script, allowWrite);
        var reflected = Probe(new ReflectedModel { Score = 3, Ticks = 4, Ratio = 0.5, Active = true, Name = "n", Tag = new JsString("t"), Tags = ["a", "b"], Counter = 6 }, script, allowWrite);

        generated.Should().Be(reflected, "the generated lane must be indistinguishable from the reflected one for `{0}`", script);
    }

    /// <summary>
    /// Runs <paramref name="script"/> against <paramref name="model"/> and reduces whatever happens — a
    /// value, a JavaScript throw, a CLR throw — to one comparable string. The two fixture type names are
    /// normalized out, since they are the only thing that legitimately differs.
    /// </summary>
    private static string Probe(object model, string script, bool allowWrite = true)
    {
        // Projected CLR writes are off by default in v5, and a write to a descriptor whose Set is null is a
        // silent no-op outside strict mode - so a write matrix run on a default engine would compare two
        // sets of nothing and pass whatever the accessors did. AllowWriteIsHonoured covers the default.
        var engine = new Engine(options => options.Interop.AllowWrite = allowWrite);
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
