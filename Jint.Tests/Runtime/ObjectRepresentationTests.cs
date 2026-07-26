using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="Engine.AdvancedOperations.GetObjectRepresentation"/> is a diagnostic, so its whole value is
/// that it cannot disagree with reality. These tests cross-check every answer it gives against the internal
/// state it claims to describe; the behavior a host can actually observe is covered in
/// Jint.Tests.PublicInterface/ObjectRepresentationTests.cs.
/// </summary>
public class ObjectRepresentationTests
{
    private static readonly JsObjectLayout Layout = new("id", "name", "active");

    private static JsObject CreateSample(Engine engine) => JsObject.Create(
        engine,
        Layout,
        [JsNumber.Create(1), JsString.Create("sample"), JsBoolean.True]);

    [Fact]
    public void ShapedAndDeoptedObjectsAgreeWithTheShapeFlag()
    {
        var engine = new Engine();
        var obj = CreateSample(engine);
        engine.SetValue("o", obj);

        (obj._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);
        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.HiddenClass);

        engine.Execute("o.extra = 1;");

        (obj._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        obj._properties.Should().NotBeNull();
        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.Dictionary);
    }

    [Fact]
    public void BuiltinsAgreeWithTheBuiltinShapeFlag()
    {
        var engine = new Engine();
        engine.Execute("Math.abs(1);");

        var math = engine.Evaluate("Math").AsObject();
        (math._type & InternalTypes.BuiltinShapeMode).Should().NotBe(InternalTypes.Empty);
        engine.Advanced.GetObjectRepresentation(math).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    [Fact]
    public void EachFallbackTriggerAgreesWithTheShapeFlag()
    {
        var engine = new Engine();

        // integer-index-like key
        var indexLike = JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("a", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("0", JsNumber.Create(2))
        ]);
        (indexLike._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        engine.Advanced.GetObjectRepresentation(indexLike).Should().Be(ObjectRepresentation.Dictionary);

        // more properties than a hidden class can describe
        var entries = new KeyValuePair<string, JsValue>[Shape.MaxShapeProperties + 1];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = new KeyValuePair<string, JsValue>("k" + i, JsNumber.Create(i));
        }

        var tooWide = JsObject.CreateFromEntries(engine, entries);
        (tooWide._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        engine.Advanced.GetObjectRepresentation(tooWide).Should().Be(ObjectRepresentation.Dictionary);
    }

    [Fact]
    public void TheFanoutGuardIsReportedExactlyWhereItTrips()
    {
        // Only the first MaxFanout distinct first-keys can branch off the empty root shape; the very next
        // object is refused and finishes as a dictionary. The diagnostic must flip at exactly that boundary.
        var engine = new Engine();

        JsObject Build(int i) => JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("k" + i, JsNumber.Create(i)),
            new KeyValuePair<string, JsValue>("shared", JsNumber.Create(i))
        ]);

        var shaped = 0;
        for (var i = 0; i < Shape.MaxFanout * 2; i++)
        {
            var representation = engine.Advanced.GetObjectRepresentation(Build(i));
            var expected = i < Shape.MaxFanout
                ? ObjectRepresentation.HiddenClass
                : ObjectRepresentation.Dictionary;

            representation.Should().Be(expected, "object {0} of an ever-widening fan-out", i);
            if (representation == ObjectRepresentation.HiddenClass)
            {
                shaped++;
            }
        }

        shaped.Should().Be(Shape.MaxFanout);
    }

    [Fact]
    public void TheHostLayoutBudgetIsReportedExactlyWhereItRunsOut()
    {
        var engine = new Engine();

        const int Width = 64; // the widest layout a hidden class can describe: fewest iterations to spend the budget
        var values = new JsValue[Width];
        for (var i = 0; i < Width; i++)
        {
            values[i] = JsNumber.Create(i);
        }

        JsObjectLayout WideLayout(string prefix)
        {
            var names = new string[Width];
            for (var i = 0; i < Width; i++)
            {
                names[i] = prefix + i;
            }

            return new JsObjectLayout(names);
        }

        var layouts = new List<JsObjectLayout>();
        for (var iteration = 0; iteration < Engine.HostShapeTransitionBudget / Width; iteration++)
        {
            var layout = WideLayout("L" + iteration + "_");
            layouts.Add(layout);
            engine.Advanced.GetObjectRepresentation(JsObject.Create(engine, layout, values))
                .Should().Be(ObjectRepresentation.HiddenClass);
        }

        // Budget spent: a layout the engine has not seen before can no longer be interned.
        engine.Advanced.GetObjectRepresentation(JsObject.Create(engine, WideLayout("overflow_"), values))
            .Should().Be(ObjectRepresentation.Dictionary);

        // An already-resolved layout still hits its memo, which never consults the budget.
        engine.Advanced.GetObjectRepresentation(JsObject.Create(engine, layouts[0], values))
            .Should().Be(ObjectRepresentation.HiddenClass);
    }
}
