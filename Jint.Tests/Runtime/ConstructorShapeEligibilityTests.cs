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
        Assert.True(IsShapeMode(o), "expected a shape-mode object");
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
        Assert.False(IsShapeMode(t1));
        Assert.False(IsShapeMode(t2));
        Assert.True(IsShapeMode(t3));
        Assert.True(IsShapeMode(t4));
        Assert.Same(ShapeOf(t3), ShapeOf(t4));

        Assert.Equal(1, engine.Evaluate("t1.a").AsNumber());
        Assert.Equal(2, engine.Evaluate("t3.b").AsNumber());
        Assert.Equal("a,b", engine.Evaluate("Object.keys(t1).join(',')").AsString());
        Assert.Equal("a,b", engine.Evaluate("Object.keys(t3).join(',')").AsString());
        Assert.True(engine.Evaluate("'a' in t3 && 'b' in t3 && !('c' in t3)").AsBoolean());
    }

    [Fact]
    public void EarlierAssignmentsAreVisibleToLaterOnesWhileShaping()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = this.a + 1; } var t1 = new T(); var t2 = new T(); var t3 = new T();");

        var t3 = GetObject(engine, "t3");
        Assert.True(IsShapeMode(t3));
        Assert.Equal(2, engine.Evaluate("t1.b").AsNumber());
        Assert.Equal(2, engine.Evaluate("t3.b").AsNumber());
    }

    [Fact]
    public void ThisFreeCallsAndBareReturnAreEligible()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.abs = Math.abs(-5); this.t = Date.now(); return; } var t1 = new T(); var t2 = new T(); var t3 = new T();");

        var t3 = GetObject(engine, "t3");
        Assert.True(IsShapeMode(t3));
        Assert.Equal(5, engine.Evaluate("t3.abs").AsNumber());
        Assert.Equal("abs,t", engine.Evaluate("Object.keys(t3).join(',')").AsString());
    }

    [Fact]
    public void DirectivePrologueDoesNotBlockEligibility()
    {
        var engine = new Engine();
        engine.Execute("function T() { 'use strict'; this.a = 1; } var t1 = new T(); var t2 = new T(); var t3 = new T();");

        var t3 = GetObject(engine, "t3");
        Assert.True(IsShapeMode(t3));
        Assert.Equal(1, engine.Evaluate("t1.a").AsNumber());
        Assert.Equal(1, engine.Evaluate("t3.a").AsNumber());
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
        Assert.True(IsShapeMode(t3));
        Assert.Equal("self", engine.Evaluate("t1.get()").AsString());
        Assert.Equal("self", engine.Evaluate("t3.get()").AsString());
    }

    [Fact]
    public void ConditionalThisAssignmentsAreEligible()
    {
        // The sunspider-3d-raytrace Triangle pattern: branches assigning the same key set.
        // Eligibility only decides how early shaping starts, so branchy-but-static bodies
        // shape from instance #3 like straight-line ones.
        var engine = new Engine();
        engine.Execute("function T(f) { if (f) this.axis = 0; else this.axis = 2; this.n = 1; } var t1 = new T(true); var t2 = new T(false); var t3 = new T(true); var t4 = new T(false);");
        Assert.False(IsShapeMode(GetObject(engine, "t1")));
        Assert.True(IsShapeMode(GetObject(engine, "t3")));
        Assert.True(IsShapeMode(GetObject(engine, "t4")));
        Assert.Same(ShapeOf(GetObject(engine, "t3")), ShapeOf(GetObject(engine, "t4")));
        Assert.Equal(0, engine.Evaluate("t3.axis").AsNumber());
        Assert.Equal(2, engine.Evaluate("t4.axis").AsNumber());
        Assert.Equal("axis,n", engine.Evaluate("Object.keys(t3).join(',')").AsString());

        // divergent key sets per branch: both layouts stay correct, shapes differ per path
        engine = new Engine();
        engine.Execute("function T(f) { if (f) { this.a = 1; } else { this.b = 2; } } var t1 = new T(true); var t2 = new T(true); var t3 = new T(true); var t4 = new T(false);");
        Assert.True(IsShapeMode(GetObject(engine, "t3")));
        Assert.True(IsShapeMode(GetObject(engine, "t4")));
        Assert.Equal(1, engine.Evaluate("t3.a").AsNumber());
        Assert.False(engine.Evaluate("'b' in t3").AsBoolean());
        Assert.Equal(2, engine.Evaluate("t4.b").AsNumber());
        Assert.False(engine.Evaluate("'a' in t4").AsBoolean());

        // a this-escaping call in the if TEST still rejects eligibility
        engine = new Engine();
        engine.Execute("function ext(o) { o.dyn = 1; return true; } function T() { if (ext(this)) this.a = 1; } var t1 = new T(); var t2 = new T(); var t3 = new T();");
        Assert.False(IsShapeMode(GetObject(engine, "t3")));
        Assert.Equal("dyn,a", engine.Evaluate("Object.keys(t3).join(',')").AsString());
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
        Assert.False(IsShapeMode(t));
        Assert.False(IsShapeMode(GetObject(engine, "t3")));
        Assert.Equal(42, engine.Evaluate("t.dyn").AsNumber());

        // this-call in body
        engine = new Engine();
        engine.Execute("function T() { this.init(); } T.prototype.init = function () { this.a = 1; }; var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.False(IsShapeMode(GetObject(engine, "t3")));
        Assert.Equal(1, engine.Evaluate("t.a").AsNumber());

        // this escaping through a call in an assignment RHS; the escapee's write lands FIRST
        engine = new Engine();
        engine.Execute("function ext(o) { o.dyn = 1; return 2; } function T() { this.a = ext(this); } var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.False(IsShapeMode(GetObject(engine, "t3")));
        Assert.Equal(2, engine.Evaluate("t.a").AsNumber());
        Assert.Equal("dyn,a", engine.Evaluate("Object.keys(t).join(',')").AsString());

        // this escaping through a call in a var initializer
        engine = new Engine();
        engine.Execute("function grab(o) { o.dyn = 1; return 3; } function T() { var x = grab(this); this.a = x; } var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.False(IsShapeMode(GetObject(engine, "t3")));
        Assert.Equal(3, engine.Evaluate("t.a").AsNumber());

        // this escaping through a parameter default (runs during construction)
        engine = new Engine();
        engine.Execute("function capture(o) { o.k = 1; return 9; } function T(a = capture(this)) { this.x = a; } var t = new T(); var t2 = new T(); var t3 = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.False(IsShapeMode(GetObject(engine, "t3")));
        Assert.Equal(9, engine.Evaluate("t.x").AsNumber());
        Assert.Equal(1, engine.Evaluate("t.k").AsNumber());
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
        Assert.False(IsShapeMode(GetObject(engine, "arr[0]")));
        Assert.False(IsShapeMode(GetObject(engine, "arr[15]")));
        Assert.True(IsShapeMode(GetObject(engine, "arr[16]")));
        Assert.True(engine.Evaluate("arr.every(function (x) { return x.a === 1; })").AsBoolean());
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
        Assert.False(IsShapeMode(a1));
        Assert.False(IsShapeMode(a2));
        Assert.True(IsShapeMode(a3));
        Assert.Same(ShapeOf(a3), ShapeOf(a4));
        Assert.Equal(1, engine.Evaluate("a1.x").AsNumber());
        Assert.Equal(1, engine.Evaluate("a3.x").AsNumber());
    }

    [Fact]
    public void ClassFieldsRunBeforeConstructorBodyAssignments()
    {
        var engine = new Engine();
        engine.Execute("class D { y = 1; constructor() { this.z = 2; } } var d1 = new D(); var d2 = new D(); var d3 = new D();");

        var d3 = GetObject(engine, "d3");
        Assert.True(IsShapeMode(d3));
        Assert.Equal("y,z", engine.Evaluate("Object.keys(d1).join(',')").AsString());
        Assert.Equal("y,z", engine.Evaluate("Object.keys(d3).join(',')").AsString());
    }

    [Fact]
    public void IndexLikeComputedFieldNameFallsBackToSamplingWithCorrectOrder()
    {
        var engine = new Engine();
        engine.Execute("class B { b = 2; ['0'] = 1 } var b1 = new B(); var b2 = new B(); var b3 = new B();");

        var b1 = GetObject(engine, "b1");
        Assert.False(IsShapeMode(b1));
        Assert.False(IsShapeMode(GetObject(engine, "b3"))); // fields check rejects — sampling path, not #3 shaping
        // integer-index keys enumerate first regardless of insertion order
        Assert.Equal("0,b", engine.Evaluate("Object.keys(b1).join(',')").AsString());
        Assert.Equal(1, engine.Evaluate("b1[0]").AsNumber());
        Assert.Equal(2, engine.Evaluate("b1.b").AsNumber());
    }

    [Fact]
    public void PrivateFieldsDoNotBlockEligibility()
    {
        var engine = new Engine();
        engine.Execute("class C { #p = 5; x = 1; getP() { return this.#p; } } var c1 = new C(); var c2 = new C(); var c3 = new C();");

        var c3 = GetObject(engine, "c3");
        Assert.True(IsShapeMode(c3));
        Assert.Equal(1, engine.Evaluate("c3.x").AsNumber());
        Assert.Equal(5, engine.Evaluate("c1.getP()").AsNumber());
        Assert.Equal(5, engine.Evaluate("c3.getP()").AsNumber());
        Assert.Equal("x", engine.Evaluate("Object.keys(c3).join(',')").AsString());
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
        Assert.False(IsShapeMode(b1));
        Assert.False(IsShapeMode(b2));
        Assert.True(IsShapeMode(b3));
        Assert.Equal(1, engine.Evaluate("b3.x").AsNumber());
        Assert.Equal(2, engine.Evaluate("b3.y").AsNumber());
        Assert.Equal("x,y", engine.Evaluate("Object.keys(b1).join(',')").AsString());
        Assert.Equal("x,y", engine.Evaluate("Object.keys(b3).join(',')").AsString());
        Assert.True(engine.Evaluate("b3 instanceof B && b3 instanceof A").AsBoolean());
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
        Assert.False(IsShapeMode(GetObject(engine, "a1")));
        Assert.False(IsShapeMode(GetObject(engine, "a2")));
        Assert.True(IsShapeMode(r));
        Assert.True(IsShapeMode(a3));
        Assert.True(engine.Evaluate("Object.getPrototypeOf(r) === C.prototype").AsBoolean());
        Assert.Equal(1, engine.Evaluate("r.x").AsNumber());
        // different prototypes root different transition trees
        Assert.NotSame(ShapeOf(r), ShapeOf(a3));
    }

    [Fact]
    public void MegamorphicGuardTripConvertsMidBuildKeepingAllPropertiesInOrder()
    {
        var assignments = string.Concat(Enumerable.Range(0, 70).Select(i => $"this.p{i} = {i};"));
        var engine = new Engine();
        engine.Execute($"function T() {{ {assignments} }} var t0 = new T(); var t1 = new T(); var t = new T();");

        // eligible, so instance #3 starts shaped — and deopts to dictionary at the 64-property guard
        var t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));

        var expectedKeys = string.Join(",", Enumerable.Range(0, 70).Select(i => $"p{i}"));
        Assert.Equal(expectedKeys, engine.Evaluate("Object.keys(t).join(',')").AsString());
        Assert.True(engine.Evaluate("(function () { for (var i = 0; i < 70; i++) { if (t['p' + i] !== i) return false; } return true; })()").AsBoolean());
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

        Assert.True(IsShapeMode(t1));
        Assert.True(IsShapeMode(t2));
        Assert.NotSame(ShapeOf(t1), ShapeOf(t2)); // different prototype, different root
        Assert.Same(ShapeOf(t2), ShapeOf(t3));

        Assert.Equal(1, engine.Evaluate("t2.a").AsNumber());
        Assert.Equal("p2", engine.Evaluate("t2.tag").AsString());
        Assert.False(engine.Evaluate("'tag' in t1").AsBoolean());
    }

    [Fact]
    public void DeleteAndDefinePropertyOnShapedInstanceDeoptCorrectly()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = 2; } var t0a = new T(); var t0b = new T(); var t = new T();");
        Assert.True(IsShapeMode(GetObject(engine, "t")));

        engine.Execute("delete t.a;");
        var t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.False(engine.Evaluate("'a' in t").AsBoolean());
        Assert.Equal(2, engine.Evaluate("t.b").AsNumber());
        Assert.Equal("b", engine.Evaluate("Object.keys(t).join(',')").AsString());

        engine.Execute("var u = new T(); Object.defineProperty(u, 'b', { enumerable: false });");
        var u = GetObject(engine, "u");
        Assert.False(IsShapeMode(u));
        Assert.Equal(2, engine.Evaluate("u.b").AsNumber());
        Assert.Equal("a", engine.Evaluate("Object.keys(u).join(',')").AsString());

        engine.Execute("var v = new T(); Object.defineProperty(v, 'a', { get: function () { return 99; } });");
        Assert.Equal(99, engine.Evaluate("v.a").AsNumber());
    }
}
