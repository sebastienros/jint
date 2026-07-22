using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins static constructor-body shape eligibility (JintFunctionDefinition.ComputeCtorBodyShapeEligibility
/// consumed by ScriptFunction's [[Construct]]): provably-clean constructors start hidden-class shape
/// building from instance #3 (instances #1 and #2 stay dictionary-mode, so constructors of unrepeated
/// layouts intern no shape state) instead of after the 16-instance sampling window, while everything else
/// keeps the sampling behavior exactly — and correctness never depends on the verdict (deopt machinery
/// unchanged).
/// </summary>
public class ConstructorShapeEligibilityTests
{
    private static JsObject GetObject(Engine engine, string expression) => (JsObject) engine.Evaluate(expression);

    private static bool IsShapeMode(JsObject o) => (o._type & InternalTypes.ShapeMode) != InternalTypes.Empty;

    private static Shape ShapeOf(JsObject o)
    {
        IsShapeMode(o).Should().BeTrue("expected a shape-mode object");
        return o.ShapeOf;
    }

    [Fact]
    public void EligibleConstructorShapesFromThirdInstance()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = 2; } var t1 = new T(); var t2 = new T(); var t3 = new T(); var t4 = new T();");

        var t1 = GetObject(engine, "t1");
        var t2 = GetObject(engine, "t2");
        var t3 = GetObject(engine, "t3");
        var t4 = GetObject(engine, "t4");

        // instances #1 and #2 stay dictionaries (the interned tree only pays off from ~3 instances,
        // so unrepeated layouts intern no shape state at all); instances #3+ share one hidden class
        IsShapeMode(t1).Should().BeFalse();
        IsShapeMode(t2).Should().BeFalse();
        IsShapeMode(t3).Should().BeTrue();
        IsShapeMode(t4).Should().BeTrue();
        ShapeOf(t4).Should().BeSameAs(ShapeOf(t3));

        engine.Evaluate("t1.a").AsNumber().Should().Be(1);
        engine.Evaluate("t3.b").AsNumber().Should().Be(2);
        engine.Evaluate("Object.keys(t1).join(',')").AsString().Should().Be("a,b");
        engine.Evaluate("Object.keys(t3).join(',')").AsString().Should().Be("a,b");
        engine.Evaluate("'a' in t3 && 'b' in t3 && !('c' in t3)").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void EarlierAssignmentsAreVisibleToLaterOnesWhileShaping()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = this.a + 1; } var t1 = new T(); var t2 = new T(); var t3 = new T();");

        var t3 = GetObject(engine, "t3");
        IsShapeMode(t3).Should().BeTrue();
        engine.Evaluate("t1.b").AsNumber().Should().Be(2);
        engine.Evaluate("t3.b").AsNumber().Should().Be(2);
    }

    [Fact]
    public void ThisFreeCallsAndBareReturnAreEligible()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.abs = Math.abs(-5); this.t = Date.now(); return; } var t1 = new T(); var t2 = new T(); var t3 = new T();");

        var t3 = GetObject(engine, "t3");
        IsShapeMode(t3).Should().BeTrue();
        engine.Evaluate("t3.abs").AsNumber().Should().Be(5);
        engine.Evaluate("Object.keys(t3).join(',')").AsString().Should().Be("abs,t");
    }

    [Fact]
    public void DirectivePrologueDoesNotBlockEligibility()
    {
        var engine = new Engine();
        engine.Execute("function T() { 'use strict'; this.a = 1; } var t1 = new T(); var t2 = new T(); var t3 = new T();");

        var t3 = GetObject(engine, "t3");
        IsShapeMode(t3).Should().BeTrue();
        engine.Evaluate("t1.a").AsNumber().Should().Be(1);
        engine.Evaluate("t3.a").AsNumber().Should().Be(1);
    }

    [Fact]
    public void SelfAliasClosurePatternIsEligible()
    {
        var engine = new Engine();
        engine.Execute("""
            function T() {
                var self = this;
                this.a = 1;
                this.get = function () { return self === this ? 'self' : 'other'; };
            }
            var t1 = new T();
            var t2 = new T();
            var t3 = new T();
            """);

        var t3 = GetObject(engine, "t3");
        IsShapeMode(t3).Should().BeTrue();
        engine.Evaluate("t1.get()").AsString().Should().Be("self");
        engine.Evaluate("t3.get()").AsString().Should().Be("self");
    }

    [Fact]
    public void ConditionalThisAssignmentsAreEligible()
    {
        // The sunspider-3d-raytrace Triangle pattern: branches assigning the same key set.
        // Eligibility only decides how early shaping starts, so branchy-but-static bodies
        // shape from instance #3 like straight-line ones.
        var engine = new Engine();
        engine.Execute("function T(f) { if (f) this.axis = 0; else this.axis = 2; this.n = 1; } var t1 = new T(true); var t2 = new T(false); var t3 = new T(true); var t4 = new T(false);");
        IsShapeMode(GetObject(engine, "t1")).Should().BeFalse();
        IsShapeMode(GetObject(engine, "t3")).Should().BeTrue();
        IsShapeMode(GetObject(engine, "t4")).Should().BeTrue();
        ShapeOf(GetObject(engine, "t4")).Should().BeSameAs(ShapeOf(GetObject(engine, "t3")));
        engine.Evaluate("t3.axis").AsNumber().Should().Be(0);
        engine.Evaluate("t4.axis").AsNumber().Should().Be(2);
        engine.Evaluate("Object.keys(t3).join(',')").AsString().Should().Be("axis,n");

        // divergent key sets per branch: both layouts stay correct, shapes differ per path
        engine = new Engine();
        engine.Execute("function T(f) { if (f) { this.a = 1; } else { this.b = 2; } } var t1 = new T(true); var t2 = new T(true); var t3 = new T(true); var t4 = new T(false);");
        IsShapeMode(GetObject(engine, "t3")).Should().BeTrue();
        IsShapeMode(GetObject(engine, "t4")).Should().BeTrue();
        engine.Evaluate("t3.a").AsNumber().Should().Be(1);
        engine.Evaluate("'b' in t3").AsBoolean().Should().BeFalse();
        engine.Evaluate("t4.b").AsNumber().Should().Be(2);
        engine.Evaluate("'a' in t4").AsBoolean().Should().BeFalse();

        // a this-escaping call in the if TEST still rejects eligibility
        engine = new Engine();
        engine.Execute("function ext(o) { o.dyn = 1; return true; } function T() { if (ext(this)) this.a = 1; } var t1 = new T(); var t2 = new T(); var t3 = new T();");
        IsShapeMode(GetObject(engine, "t3")).Should().BeFalse();
        engine.Evaluate("Object.keys(t3).join(',')").AsString().Should().Be("dyn,a");
    }

    [Fact]
    public void IneligibleBodiesBehaveExactlyAsBeforeAndStayUnshaped()
    {
        // Ineligible constructors keep the sampling window: with eligible ones now shaping from
        // instance #3, asserting instance #3 is still dictionary-mode is what distinguishes them.

        // computed LHS
        var engine = new Engine();
        engine.Execute("function T(k) { this[k] = 42; } var t = new T('dyn'); var t2 = new T('dyn'); var t3 = new T('dyn');");
        var t = GetObject(engine, "t");
        IsShapeMode(t).Should().BeFalse();
        IsShapeMode(GetObject(engine, "t3")).Should().BeFalse();
        engine.Evaluate("t.dyn").AsNumber().Should().Be(42);

        // this-call in body
        engine = new Engine();
        engine.Execute("function T() { this.init(); } T.prototype.init = function () { this.a = 1; }; var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        IsShapeMode(t).Should().BeFalse();
        IsShapeMode(GetObject(engine, "t3")).Should().BeFalse();
        engine.Evaluate("t.a").AsNumber().Should().Be(1);

        // this escaping through a call in an assignment RHS; the escapee's write lands FIRST
        engine = new Engine();
        engine.Execute("function ext(o) { o.dyn = 1; return 2; } function T() { this.a = ext(this); } var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        IsShapeMode(t).Should().BeFalse();
        IsShapeMode(GetObject(engine, "t3")).Should().BeFalse();
        engine.Evaluate("t.a").AsNumber().Should().Be(2);
        engine.Evaluate("Object.keys(t).join(',')").AsString().Should().Be("dyn,a");

        // this escaping through a call in a var initializer
        engine = new Engine();
        engine.Execute("function grab(o) { o.dyn = 1; return 3; } function T() { var x = grab(this); this.a = x; } var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        IsShapeMode(t).Should().BeFalse();
        IsShapeMode(GetObject(engine, "t3")).Should().BeFalse();
        engine.Evaluate("t.a").AsNumber().Should().Be(3);

        // this escaping through a parameter default (runs during construction)
        engine = new Engine();
        engine.Execute("function capture(o) { o.k = 1; return 9; } function T(a = capture(this)) { this.x = a; } var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        IsShapeMode(t).Should().BeFalse();
        IsShapeMode(GetObject(engine, "t3")).Should().BeFalse();
        engine.Evaluate("t.x").AsNumber().Should().Be(9);
        engine.Evaluate("t.k").AsNumber().Should().Be(1);
    }

    [Fact]
    public void IneligibleConstructorKeepsSamplingThresholdPacing()
    {
        var engine = new Engine();
        engine.Execute("""
            function T(k) { this[k] = 1; }
            var arr = [];
            for (var i = 0; i < 17; i++) { arr.push(new T('a')); }
            """);

        // instances 1..16 build dictionaries (the threshold-tripping 16th included); the 17th starts shaped
        IsShapeMode(GetObject(engine, "arr[0]")).Should().BeFalse();
        IsShapeMode(GetObject(engine, "arr[15]")).Should().BeFalse();
        IsShapeMode(GetObject(engine, "arr[16]")).Should().BeTrue();
        engine.Evaluate("arr.every(function (x) { return x.a === 1; })").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClassFieldOnlyInstanceShapesFromThirdInstance()
    {
        var engine = new Engine();
        engine.Execute("class A { x = 1 } var a1 = new A(); var a2 = new A(); var a3 = new A(); var a4 = new A();");

        var a1 = GetObject(engine, "a1");
        var a2 = GetObject(engine, "a2");
        var a3 = GetObject(engine, "a3");
        var a4 = GetObject(engine, "a4");
        IsShapeMode(a1).Should().BeFalse();
        IsShapeMode(a2).Should().BeFalse();
        IsShapeMode(a3).Should().BeTrue();
        ShapeOf(a4).Should().BeSameAs(ShapeOf(a3));
        engine.Evaluate("a1.x").AsNumber().Should().Be(1);
        engine.Evaluate("a3.x").AsNumber().Should().Be(1);
    }

    [Fact]
    public void ClassFieldsRunBeforeConstructorBodyAssignments()
    {
        var engine = new Engine();
        engine.Execute("class D { y = 1; constructor() { this.z = 2; } } var d1 = new D(); var d2 = new D(); var d3 = new D();");

        var d3 = GetObject(engine, "d3");
        IsShapeMode(d3).Should().BeTrue();
        engine.Evaluate("Object.keys(d1).join(',')").AsString().Should().Be("y,z");
        engine.Evaluate("Object.keys(d3).join(',')").AsString().Should().Be("y,z");
    }

    [Fact]
    public void IndexLikeComputedFieldNameFallsBackToSamplingWithCorrectOrder()
    {
        var engine = new Engine();
        engine.Execute("class B { b = 2; ['0'] = 1 } var b1 = new B(); var b2 = new B(); var b3 = new B();");

        var b1 = GetObject(engine, "b1");
        IsShapeMode(b1).Should().BeFalse();
        IsShapeMode(GetObject(engine, "b3")).Should().BeFalse(); // fields check rejects — sampling path, not #3 shaping
        // integer-index keys enumerate first regardless of insertion order
        engine.Evaluate("Object.keys(b1).join(',')").AsString().Should().Be("0,b");
        engine.Evaluate("b1[0]").AsNumber().Should().Be(1);
        engine.Evaluate("b1.b").AsNumber().Should().Be(2);
    }

    [Fact]
    public void PrivateFieldsDoNotBlockEligibility()
    {
        var engine = new Engine();
        engine.Execute("class C { #p = 5; x = 1; getP() { return this.#p; } } var c1 = new C(); var c2 = new C(); var c3 = new C();");

        var c3 = GetObject(engine, "c3");
        IsShapeMode(c3).Should().BeTrue();
        engine.Evaluate("c3.x").AsNumber().Should().Be(1);
        engine.Evaluate("c1.getP()").AsNumber().Should().Be(5);
        engine.Evaluate("c3.getP()").AsNumber().Should().Be(5);
        engine.Evaluate("Object.keys(c3).join(',')").AsString().Should().Be("x");
    }

    [Fact]
    public void DerivedConstructorOverEligibleBaseShapesFromThirdInstance()
    {
        var engine = new Engine();
        engine.Execute("""
            class A { constructor() { this.x = 1; } }
            class B extends A { constructor() { super(); this.y = 2; } }
            var b1 = new B();
            var b2 = new B();
            var b3 = new B();
            """);

        // eligibility lives on the allocating BASE constructor: B's first two constructions promote A,
        // B's third construction allocates a shaping `this`
        var b1 = GetObject(engine, "b1");
        var b2 = GetObject(engine, "b2");
        var b3 = GetObject(engine, "b3");
        IsShapeMode(b1).Should().BeFalse();
        IsShapeMode(b2).Should().BeFalse();
        IsShapeMode(b3).Should().BeTrue();
        engine.Evaluate("b3.x").AsNumber().Should().Be(1);
        engine.Evaluate("b3.y").AsNumber().Should().Be(2);
        engine.Evaluate("Object.keys(b1).join(',')").AsString().Should().Be("x,y");
        engine.Evaluate("Object.keys(b3).join(',')").AsString().Should().Be("x,y");
        engine.Evaluate("b3 instanceof B && b3 instanceof A").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReflectConstructWithForeignNewTargetUsesForeignPrototype()
    {
        var engine = new Engine();
        engine.Execute("""
            function A() { this.x = 1; }
            function C() {}
            var a1 = new A();                    // #1: dictionary
            var a2 = new A();                    // #2: dictionary; promotes A
            var r = Reflect.construct(A, [], C); // #3: shaped against C.prototype's root
            var a3 = new A();                    // #4: shaped against A.prototype's root
            """);

        var r = GetObject(engine, "r");
        var a3 = GetObject(engine, "a3");
        IsShapeMode(GetObject(engine, "a1")).Should().BeFalse();
        IsShapeMode(GetObject(engine, "a2")).Should().BeFalse();
        IsShapeMode(r).Should().BeTrue();
        IsShapeMode(a3).Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(r) === C.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.x").AsNumber().Should().Be(1);
        // different prototypes root different transition trees
        ShapeOf(a3).Should().NotBeSameAs(ShapeOf(r));
    }

    [Fact]
    public void MegamorphicGuardTripConvertsMidBuildKeepingAllPropertiesInOrder()
    {
        var assignments = string.Concat(Enumerable.Range(0, 70).Select(i => $"this.p{i} = {i};"));
        var engine = new Engine();
        engine.Execute($"function T() {{ {assignments} }} var t0 = new T(); var t1 = new T(); var t = new T();");

        // eligible, so instance #3 starts shaped — and deopts to dictionary at the 64-property guard
        var t = GetObject(engine, "t");
        IsShapeMode(t).Should().BeFalse();

        var expectedKeys = string.Join(",", Enumerable.Range(0, 70).Select(i => $"p{i}"));
        engine.Evaluate("Object.keys(t).join(',')").AsString().Should().Be(expectedKeys);
        engine.Evaluate("(function () { for (var i = 0; i < 70; i++) { if (t['p' + i] !== i) return false; } return true; })()").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void PrototypeReassignmentBetweenConstructionsIsHonoured()
    {
        var engine = new Engine();
        engine.Execute("""
            function T() { this.a = 1; }
            var t0a = new T();
            var t0b = new T();
            var t1 = new T();
            T.prototype = { tag: 'p2' };
            var t2 = new T();
            var t3 = new T();
            """);

        var t1 = GetObject(engine, "t1");
        var t2 = GetObject(engine, "t2");
        var t3 = GetObject(engine, "t3");

        IsShapeMode(t1).Should().BeTrue();
        IsShapeMode(t2).Should().BeTrue();
        ShapeOf(t2).Should().NotBeSameAs(ShapeOf(t1)); // different prototype, different root
        ShapeOf(t3).Should().BeSameAs(ShapeOf(t2));

        engine.Evaluate("t2.a").AsNumber().Should().Be(1);
        engine.Evaluate("t2.tag").AsString().Should().Be("p2");
        engine.Evaluate("'tag' in t1").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void DeleteAndDefinePropertyOnShapedInstanceDeoptCorrectly()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = 2; } var t0a = new T(); var t0b = new T(); var t = new T();");
        IsShapeMode(GetObject(engine, "t")).Should().BeTrue();

        engine.Execute("delete t.a;");
        var t = GetObject(engine, "t");
        IsShapeMode(t).Should().BeFalse();
        engine.Evaluate("'a' in t").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.b").AsNumber().Should().Be(2);
        engine.Evaluate("Object.keys(t).join(',')").AsString().Should().Be("b");

        engine.Execute("var u = new T(); Object.defineProperty(u, 'b', { enumerable: false });");
        var u = GetObject(engine, "u");
        IsShapeMode(u).Should().BeFalse();
        engine.Evaluate("u.b").AsNumber().Should().Be(2);
        engine.Evaluate("Object.keys(u).join(',')").AsString().Should().Be("a");

        engine.Execute("var v = new T(); Object.defineProperty(v, 'a', { get: function () { return 99; } });");
        engine.Evaluate("v.a").AsNumber().Should().Be(99);
    }
}
