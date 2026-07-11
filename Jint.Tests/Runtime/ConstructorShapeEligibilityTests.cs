using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins static constructor-body shape eligibility (JintFunctionDefinition.ComputeCtorBodyShapeEligibility
/// consumed by ScriptFunction's [[Construct]]): provably-clean constructors start hidden-class shape
/// building from instance #1 instead of after the 16-instance sampling window, while everything else keeps
/// the sampling behavior exactly — and correctness never depends on the verdict (deopt machinery unchanged).
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
    public void EligibleConstructorShapesFromFirstInstance()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = 2; } var t1 = new T(); var t2 = new T();");

        var t1 = GetObject(engine, "t1");
        var t2 = GetObject(engine, "t2");

        Assert.True(IsShapeMode(t1));
        Assert.True(IsShapeMode(t2));
        Assert.Same(ShapeOf(t1), ShapeOf(t2));

        Assert.Equal(1, engine.Evaluate("t1.a").AsNumber());
        Assert.Equal(2, engine.Evaluate("t1.b").AsNumber());
        Assert.Equal("a,b", engine.Evaluate("Object.keys(t1).join(',')").AsString());
        Assert.True(engine.Evaluate("'a' in t1 && 'b' in t1 && !('c' in t1)").AsBoolean());
    }

    [Fact]
    public void EarlierAssignmentsAreVisibleToLaterOnesWhileShaping()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = this.a + 1; } var t = new T();");

        var t = GetObject(engine, "t");
        Assert.True(IsShapeMode(t));
        Assert.Equal(2, engine.Evaluate("t.b").AsNumber());
    }

    [Fact]
    public void ThisFreeCallsAndBareReturnAreEligible()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.abs = Math.abs(-5); this.t = Date.now(); return; } var t = new T();");

        var t = GetObject(engine, "t");
        Assert.True(IsShapeMode(t));
        Assert.Equal(5, engine.Evaluate("t.abs").AsNumber());
        Assert.Equal("abs,t", engine.Evaluate("Object.keys(t).join(',')").AsString());
    }

    [Fact]
    public void DirectivePrologueDoesNotBlockEligibility()
    {
        var engine = new Engine();
        engine.Execute("function T() { 'use strict'; this.a = 1; } var t = new T();");

        var t = GetObject(engine, "t");
        Assert.True(IsShapeMode(t));
        Assert.Equal(1, engine.Evaluate("t.a").AsNumber());
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
            var t = new T();
            """);

        var t = GetObject(engine, "t");
        Assert.True(IsShapeMode(t));
        Assert.Equal("self", engine.Evaluate("t.get()").AsString());
    }

    [Fact]
    public void IneligibleBodiesBehaveExactlyAsBeforeAndStayUnshapedAtFirstInstance()
    {
        // conditional assignment
        var engine = new Engine();
        engine.Execute("function T(f) { if (f) { this.a = 1; } else { this.b = 2; } } var t = new T(true);");
        var t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.Equal(1, engine.Evaluate("t.a").AsNumber());
        Assert.False(engine.Evaluate("'b' in t").AsBoolean());

        // computed LHS
        engine = new Engine();
        engine.Execute("function T(k) { this[k] = 42; } var t = new T('dyn');");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.Equal(42, engine.Evaluate("t.dyn").AsNumber());

        // this-call in body
        engine = new Engine();
        engine.Execute("function T() { this.init(); } T.prototype.init = function () { this.a = 1; }; var t = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.Equal(1, engine.Evaluate("t.a").AsNumber());

        // this escaping through a call in an assignment RHS; the escapee's write lands FIRST
        engine = new Engine();
        engine.Execute("function ext(o) { o.dyn = 1; return 2; } function T() { this.a = ext(this); } var t = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.Equal(2, engine.Evaluate("t.a").AsNumber());
        Assert.Equal("dyn,a", engine.Evaluate("Object.keys(t).join(',')").AsString());

        // this escaping through a call in a var initializer
        engine = new Engine();
        engine.Execute("function grab(o) { o.dyn = 1; return 3; } function T() { var x = grab(this); this.a = x; } var t = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
        Assert.Equal(3, engine.Evaluate("t.a").AsNumber());

        // this escaping through a parameter default (runs during construction)
        engine = new Engine();
        engine.Execute("function capture(o) { o.k = 1; return 9; } function T(a = capture(this)) { this.x = a; } var t = new T();");
        t = GetObject(engine, "t");
        Assert.False(IsShapeMode(t));
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
    public void ClassFieldOnlyInstanceShapesFromFirstInstance()
    {
        var engine = new Engine();
        engine.Execute("class A { x = 1 } var a1 = new A(); var a2 = new A();");

        var a1 = GetObject(engine, "a1");
        var a2 = GetObject(engine, "a2");
        Assert.True(IsShapeMode(a1));
        Assert.Same(ShapeOf(a1), ShapeOf(a2));
        Assert.Equal(1, engine.Evaluate("a1.x").AsNumber());
    }

    [Fact]
    public void ClassFieldsRunBeforeConstructorBodyAssignments()
    {
        var engine = new Engine();
        engine.Execute("class D { y = 1; constructor() { this.z = 2; } } var d = new D();");

        var d = GetObject(engine, "d");
        Assert.True(IsShapeMode(d));
        Assert.Equal("y,z", engine.Evaluate("Object.keys(d).join(',')").AsString());
    }

    [Fact]
    public void IndexLikeComputedFieldNameFallsBackToSamplingWithCorrectOrder()
    {
        var engine = new Engine();
        engine.Execute("class B { b = 2; ['0'] = 1 } var b1 = new B();");

        var b1 = GetObject(engine, "b1");
        Assert.False(IsShapeMode(b1));
        // integer-index keys enumerate first regardless of insertion order
        Assert.Equal("0,b", engine.Evaluate("Object.keys(b1).join(',')").AsString());
        Assert.Equal(1, engine.Evaluate("b1[0]").AsNumber());
        Assert.Equal(2, engine.Evaluate("b1.b").AsNumber());
    }

    [Fact]
    public void PrivateFieldsDoNotBlockEligibility()
    {
        var engine = new Engine();
        engine.Execute("class C { #p = 5; x = 1; getP() { return this.#p; } } var c = new C();");

        var c = GetObject(engine, "c");
        Assert.True(IsShapeMode(c));
        Assert.Equal(1, engine.Evaluate("c.x").AsNumber());
        Assert.Equal(5, engine.Evaluate("c.getP()").AsNumber());
        Assert.Equal("x", engine.Evaluate("Object.keys(c).join(',')").AsString());
    }

    [Fact]
    public void DerivedConstructorOverEligibleBaseShapesFromFirstInstance()
    {
        var engine = new Engine();
        engine.Execute("""
            class A { constructor() { this.x = 1; } }
            class B extends A { constructor() { super(); this.y = 2; } }
            var b = new B();
            """);

        var b = GetObject(engine, "b");
        Assert.True(IsShapeMode(b));
        Assert.Equal(1, engine.Evaluate("b.x").AsNumber());
        Assert.Equal(2, engine.Evaluate("b.y").AsNumber());
        Assert.Equal("x,y", engine.Evaluate("Object.keys(b).join(',')").AsString());
        Assert.True(engine.Evaluate("b instanceof B && b instanceof A").AsBoolean());
    }

    [Fact]
    public void ReflectConstructWithForeignNewTargetUsesForeignPrototype()
    {
        var engine = new Engine();
        engine.Execute("""
            function A() { this.x = 1; }
            function C() {}
            var r = Reflect.construct(A, [], C);
            var a = new A();
            """);

        var r = GetObject(engine, "r");
        var a = GetObject(engine, "a");
        Assert.True(IsShapeMode(r));
        Assert.True(IsShapeMode(a));
        Assert.True(engine.Evaluate("Object.getPrototypeOf(r) === C.prototype").AsBoolean());
        Assert.Equal(1, engine.Evaluate("r.x").AsNumber());
        // different prototypes root different transition trees
        Assert.NotSame(ShapeOf(r), ShapeOf(a));
    }

    [Fact]
    public void MegamorphicGuardTripConvertsMidBuildKeepingAllPropertiesInOrder()
    {
        var assignments = string.Concat(Enumerable.Range(0, 70).Select(i => $"this.p{i} = {i};"));
        var engine = new Engine();
        engine.Execute($"function T() {{ {assignments} }} var t = new T();");

        // eligible, so instance #1 starts shaped — and deopts to dictionary at the 64-property guard
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
    public void DeleteAndDefinePropertyOnShapedFirstInstanceDeoptCorrectly()
    {
        var engine = new Engine();
        engine.Execute("function T() { this.a = 1; this.b = 2; } var t = new T();");
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
