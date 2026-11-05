#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// When a decorator is applied, what the class binding, a static field and a static block see afterwards, and what
/// the context object a decorator gets holds, against the decorators spec text,
/// https://github.com/tc39/ecma262/pull/2417 (sebastienros/jint#4413).
/// <para>
/// <see href="https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation">ClassDefinitionEvaluation</see>
/// evaluates every element first, its decorators and its key included, and only then applies the element decorators:
/// those of the static methods, getters, setters and accessors, then those of the instance ones, then those of the
/// static fields, then those of the instance fields, each group in source order. It applies the class's own
/// decorators next, before any static field or static block runs, initializes the class binding with what they
/// returned, and runs the static half of the class against that class. The context object an element decorator gets
/// is CreateDecoratorContextObject's: <c>kind</c>, <c>access</c>, <c>static</c>, <c>private</c>, <c>name</c> and
/// <c>addInitializer</c>, in that order, a class decorator's only <c>kind</c>, <c>name</c> and
/// <c>addInitializer</c>; <c>access</c> is CreateDecoratorAccessObject's <c>get</c>, <c>set</c> and <c>has</c>.
/// </para>
/// <para>
/// Node has no decorators, so the expected values follow the spec text. Each was also run through Babel's
/// implementation of it (<c>@babel/plugin-proposal-decorators</c>, version <c>2023-11</c>), which agrees except where
/// a test says otherwise.
/// </para>
/// </summary>
public class DecoratorApplicationTests
{
    /// <summary>
    /// <c>dec(name)</c> logs <c>eval name</c> when the decorator expression is evaluated, and the decorator it returns
    /// logs <c>name</c> when it is applied; <c>key(name)</c> logs a computed key.
    /// </summary>
    private const string Log = """
        var order = [];
        function log(entry) { order.push(entry); return entry; }
        function dec(name) { log('eval ' + name); return function () { log(name); }; }
        function key(name) { return log(name); }
        """;

    private static JsValue Evaluate(string script) => new Engine().Evaluate(Log + "\n" + script);

    private static string Run(string script) => Evaluate(script).AsString();

    // ------------------------------------------------------------------ element decorators: when

    /// <summary>
    /// An element's decorators are evaluated with it, and applied once every element has been evaluated.
    /// </summary>
    [Test]
    public void AnElementDecoratorIsAppliedAfterEveryElementIsEvaluated()
    {
        // `static` keeps a computed key from reading as a member access on the decorator expression before it.
        Run("""
            class C {
                @(dec('d1')) static [key('k1')]() {}
                static [key('k2')] = 1;
                @(dec('d3')) static [key('k3')]() {}
            }
            order.join();
            """).Should().Be("eval d1,k1,k2,eval d3,k3,d1,d3");
    }

    [Test]
    public void ElementDecoratorsAreAppliedStaticNonFieldsThenInstanceNonFieldsThenFields()
    {
        Run("""
            class C {
                @(dec('instance field')) x = 1;
                @(dec('static field')) static y = 1;
                @(dec('instance method')) m() {}
                @(dec('static method')) static s() {}
                @(dec('instance getter')) get g() { return 1; }
                @(dec('static setter')) static set t(v) {}
                @(dec('instance accessor')) accessor a;
                @(dec('static accessor')) static accessor b;
                @(dec('second instance field')) z = 1;
            }
            order.filter(function (e) { return e.indexOf('eval ') !== 0; }).join();
            """).Should().Be(
            "static method,static setter,static accessor,"
            + "instance method,instance getter,instance accessor,"
            + "static field,"
            + "instance field,second instance field");
    }

    [Test]
    public void ElementDecoratorsAreAppliedBeforeTheClassDecorators()
    {
        Run("""
            @(dec('class')) class C {
                @(dec('field')) x;
                @(dec('method')) m() {}
            }
            order.join();
            """).Should().Be("eval class,eval field,eval method,method,field,class");
    }

    /// <summary>
    /// A decorator that throws ends the class there: the decorators after it are not applied, and no static block
    /// runs. Every element, the decorators of the ones after it included, was evaluated first.
    /// </summary>
    [Test]
    public void AnElementDecoratorThatThrowsIsAppliedAfterTheLaterElementsAreEvaluated()
    {
        Run("""
            function boom() { throw new Error('boom'); }
            try {
                class C {
                    @boom static a() {}
                    @(dec('b')) static b() {}
                    static { log('block'); }
                }
            } catch (e) {
                log('caught ' + e.message);
            }
            order.join();
            """).Should().Be("eval b,caught boom");
    }

    // ------------------------------------------------------------------ element decorators: what they leave

    /// <summary>
    /// The decorated method is defined with its element, so a later element with the same key still replaces it.
    /// Babel defines every undecorated method first and answers <c>decorated</c>; the spec text's DefineMethodProperty
    /// runs element by element.
    /// </summary>
    [Test]
    public void ALaterElementWithTheSameKeyStillReplacesADecoratedOne()
    {
        Run("""
            function replace() { return function () { return 'decorated'; }; }
            class C { @replace m() { return 'first'; } m() { return 'second'; } }
            class D { m() { return 'first'; } @replace m() { return 'second'; } }
            new C().m() + ',' + new D().m();
            """).Should().Be("second,decorated");
    }

    [Test]
    public void ADecoratedGetterKeepsTheSetterOfTheSameName()
    {
        Run("""
            function wrap(getter) { return function () { return 'wrapped ' + getter.call(this); }; }
            class C {
                @wrap get x() { return 'x'; }
                set x(v) { this.v = v; }
            }
            var c = new C();
            c.x = 1;
            var desc = Object.getOwnPropertyDescriptor(C.prototype, 'x');
            [c.x, c.v, typeof desc.set, desc.enumerable, desc.configurable].join();
            """).Should().Be("wrapped x,1,function,false,true");
    }

    [Test]
    public void DecoratingAMethodDoesNotMoveItsProperty()
    {
        Run("""
            function replace() { return function () {}; }
            class C { @replace a() {} b() {} @replace static s() {} static t() {} @replace c() {} }
            Object.getOwnPropertyNames(C.prototype).join() + ' ' + Object.getOwnPropertyNames(C).filter(function (n) { return n === 's' || n === 't'; }).join();
            """).Should().Be("constructor,a,b,c s,t");
    }

    /// <summary>
    /// ApplyDecoratorsToElementDefinition prepends each initializer a field decorator returns, and the decorators are
    /// applied last to first, so the outermost decorator's initializer gets the field's value first. That is what the
    /// spec text intends rather than what it says: read literally, the field's own initializer, appended to
    /// [[Initializers]] before the decorators' are prepended, runs last and its 3 wins. Babel and the test262 tests
    /// proposed for decorators give 8, and https://github.com/tc39/proposal-decorators/issues/569 reports the text as
    /// a bug.
    /// </summary>
    [Test]
    public void TheOutermostFieldDecoratorsInitializerRunsFirst()
    {
        Run("""
            function add1() { return function (v) { return v + 1; }; }
            function times2() { return function (v) { return v * 2; }; }
            class C { @add1 @times2 x = 3; }
            String(new C().x);
            """).Should().Be("8");
    }

    [Test]
    public void TheOutermostAccessorDecoratorsInitRunsFirst()
    {
        Run("""
            function add1() { return { init: function (v) { return v + 1; } }; }
            function times2() { return { init: function (v) { return v * 2; } }; }
            class C { @add1 @times2 accessor x = 3; @add1 @times2 static accessor y = 3; }
            new C().x + ',' + C.y;
            """).Should().Be("8,8");
    }

    [TestCase("{ get: 1 }")]
    [TestCase("{ set: 'no' }")]
    [TestCase("{ init: {} }")]
    [TestCase("5")]
    public void AnAccessorDecoratorThatReturnsSomethingNotCallableThrows(string returned)
    {
        Run($$"""
            function d() { return {{returned}}; }
            var result;
            try {
                class C { @d accessor x; }
                result = 'no throw';
            } catch (e) {
                result = e.constructor.name;
            }
            result;
            """).Should().Be("TypeError");
    }

    // ------------------------------------------------------------------ class decorators: when

    [Test]
    public void ClassDecoratorsAreAppliedBeforeTheStaticFieldsAndBlocksRun()
    {
        Run("""
            @(dec('class')) class C {
                static x = log('static field');
                static { log('static block'); }
                @(dec('static method')) static m() {}
            }
            order.filter(function (e) { return e.indexOf('eval ') !== 0; }).join();
            """).Should().Be("static method,class,static field,static block");
    }

    [Test]
    public void AClassDecoratorThatThrowsRunsNoStaticBlock()
    {
        Run("""
            function boom() { throw new Error('boom'); }
            try {
                @boom class C { static { log('block'); } static x = log('field'); }
            } catch (e) {
                log('caught ' + e.message);
            }
            order.join();
            """).Should().Be("caught boom");
    }

    // ------------------------------------------------------------------ class decorators: what the class sees

    private const string Replace = """
        var Replacement;
        function replace(c) { Replacement = class extends c {}; return Replacement; }
        """;

    [Test]
    public void AStaticBlockSeesTheDecoratedClass()
    {
        Run(Replace + """
            var seen;
            @replace class C { static { seen = [this === Replacement, C === Replacement]; } }
            seen.join() + ',' + (C === Replacement);
            """).Should().Be("true,true,true");
    }

    /// <summary>
    /// A static field's initializer runs with the decorated class as <c>this</c>, and the field is defined on that
    /// class rather than on the one the decorator replaced.
    /// </summary>
    [Test]
    public void AStaticFieldIsDefinedOnTheDecoratedClass()
    {
        Run(Replace + """
            @replace class C { static viaThis = this === Replacement; static viaBinding = C === Replacement; }
            var original = Object.getPrototypeOf(Replacement);
            [C.viaThis, C.viaBinding, Object.hasOwn(Replacement, 'viaThis'), Object.hasOwn(original, 'viaThis')].join();
            """).Should().Be("true,true,true,false");
    }

    [Test]
    public void TheClassBindingInsideTheClassIsTheDecoratedClass()
    {
        Run(Replace + """
            @replace class C { self() { return C; } static staticSelf() { return C; } }
            [new C().self() === Replacement, C.staticSelf() === Replacement].join();
            """).Should().Be("true,true");
    }

    [Test]
    public void ADecoratedClassExpressionBindsItsOwnNameToTheDecoratedClass()
    {
        Run(Replace + """
            var D = @replace class C { static self = C; };
            [D === Replacement, D.self === Replacement].join();
            """).Should().Be("true,true");
    }

    /// <summary>
    /// The initializers a static method's decorator adds run with the decorated class before the static fields, a
    /// static field's own right after that field, and the class decorator's after the last static block. Babel runs
    /// the static method's with the class the decorator replaced; the spec text runs them with the class that
    /// replaced it.
    /// </summary>
    [Test]
    public void StaticInitializersRunWhereTheSpecTextPutsThem()
    {
        Run(Replace + """
            function withInit(name) {
                return function (value, context) {
                    context.addInitializer(function () { log('init ' + name + ' ' + (this === Replacement)); });
                };
            }
            @(withInit('class')) @replace class C {
                @(withInit('static method')) static m() {}
                @(withInit('static field')) static a = log('a');
                static b = log('b');
                static { log('block'); }
            }
            order.join();
            """).Should().Be("init static method true,a,init static field true,b,block,init class true");
    }

    // ------------------------------------------------------------------ the context object

    private const string Capture = """
        var contexts = {};
        function capture(value, context) {
            contexts[(context.static ? 'static ' : '') + context.kind + (context.private ? ' private' : '')] = context;
        }
        """;

    [Test]
    public void AnElementContextHoldsItsMembersInTheSpecTextsOrder()
    {
        Run(Capture + """
            @capture class C { @capture m() {} @capture static #f; }
            [Object.keys(contexts['method']).join('/'), Object.keys(contexts['static field private']).join('/'), Object.keys(contexts['class']).join('/')].join(' ');
            """).Should().Be("kind/access/static/private/name/addInitializer kind/access/static/private/name/addInitializer kind/name/addInitializer");
    }

    [Test]
    public void APrivateElementsContextNameIsItsPrivateName()
    {
        Run(Capture + """
            class C { @capture #m() {} @capture static #f; @capture accessor #a; @capture get #g() { return 1; } }
            ['method private', 'static field private', 'accessor private', 'getter private'].map(function (k) { return contexts[k].name; }).join();
            """).Should().Be("#m,#f,#a,#g");
    }

    [Test]
    public void AccessHasTheMembersTheElementKindAllows()
    {
        Run(Capture + """
            class C {
                @capture m() {} @capture get g() { return 1; } @capture set s(v) {} @capture f; @capture accessor a;
                @capture #m() {} @capture get #g() { return 1; } @capture set #s(v) {} @capture #f; @capture accessor #a;
            }
            Object.keys(contexts).sort().map(function (k) { return k + ':' + Object.keys(contexts[k].access).join('/'); }).join(' ');
            """).Should().Be(
            "accessor:get/set/has accessor private:get/set/has field:get/set/has field private:get/set/has "
            + "getter:get/has getter private:get/has method:get/has method private:get/has "
            + "setter:set/has setter private:set/has");
    }

    [Test]
    public void AccessReadsWritesAndChecksAPublicElement()
    {
        Run(Capture + """
            class C {
                @capture m() { return 'm'; }
                @capture f = 'f';
                @capture accessor a = 'a';
                @capture get g() { return 'g ' + this.f; }
                @capture set s(v) { this.written = v; }
            }
            var o = new C();
            var r = [];
            r.push(contexts['method'].access.get(o)());
            r.push(contexts['field'].access.get(o));
            contexts['field'].access.set(o, 'F');
            r.push(o.f);
            r.push(contexts['accessor'].access.get(o));
            r.push(String(contexts['accessor'].access.set(o, 'A')));
            r.push(o.a);
            r.push(contexts['getter'].access.get(o));
            contexts['setter'].access.set(o, 'S');
            r.push(o.written);
            r.push(contexts['method'].access.has(o), contexts['method'].access.has(Object.create(o)), contexts['method'].access.has({}));
            r.push(contexts['field'].access.has(o), contexts['field'].access.has(C.prototype));
            r.join();
            """).Should().Be("m,f,F,a,undefined,A,g F,S,true,true,false,true,false");
    }

    [Test]
    public void AccessReadsWritesAndChecksAPrivateElement()
    {
        Run(Capture + """
            class C {
                @capture #m() { return 'm'; }
                @capture #f = 'f';
                @capture accessor #a = 'a';
                @capture get #g() { return 'g ' + this.#f; }
                @capture set #s(v) { this.written = v; }
                @capture static #sf = 'sf';
            }
            var o = new C();
            var r = [];
            r.push(contexts['method private'].access.get(o)());
            r.push(contexts['field private'].access.get(o));
            contexts['field private'].access.set(o, 'F');
            r.push(contexts['field private'].access.get(o));
            contexts['accessor private'].access.set(o, 'A');
            r.push(contexts['accessor private'].access.get(o));
            r.push(contexts['getter private'].access.get(o));
            contexts['setter private'].access.set(o, 'S');
            r.push(o.written);
            r.push(contexts['static field private'].access.get(C));
            r.push(contexts['method private'].access.has(o), contexts['method private'].access.has(Object.create(o)), contexts['field private'].access.has({}));
            r.join();
            """).Should().Be("m,f,F,A,g F,S,sf,true,false,false");
    }

    /// <summary>
    /// A private getter and a setter of one name are one private element, which the access of either half reaches,
    /// and decorating one half leaves the other in place.
    /// </summary>
    [Test]
    public void EitherHalfOfAPrivateGetterSetterPairReachesTheOneElement()
    {
        Run("""
            var access;
            function keep(value, context) { access = context.access; }
            function wrap(getter) { return function () { return 'wrapped ' + getter.call(this); }; }
            class C {
                #v = 0;
                get #x() { return this.#v; }
                @keep set #x(v) { this.#v = v; }
                read() { return this.#x; }
            }
            class D {
                #v = 1;
                @wrap get #x() { return this.#v; }
                set #x(v) { this.#v = v; }
                read() { return this.#x; }
                write(v) { this.#x = v; }
            }
            var c = new C();
            access.set(c, 5);
            var d = new D();
            d.write(7);
            [c.read(), access.has(c), access.has({}), d.read()].join();
            """).Should().Be("5,true,false,wrapped 7");
    }

    [TestCase("access.get(1)")]
    [TestCase("access.set(undefined, 1)")]
    [TestCase("access.has('x')")]
    [TestCase("privateAccess.get({})")]
    [TestCase("privateAccess.set({}, 1)")]
    public void AccessThrowsATypeErrorForAnObjectItCannotReach(string call)
    {
        Run(Capture + $$"""
            class C { @capture f; @capture #f; }
            var access = contexts['field'].access, privateAccess = contexts['field private'].access;
            var result = typeof access + ' ' + typeof privateAccess + ' ';
            try {
                {{call}};
                result += 'no throw';
            } catch (e) {
                result += e.constructor.name;
            }
            result;
            """).Should().Be("object object TypeError");
    }

    [Test]
    public void AccessFunctionsHaveTheSpecTextsNamesAndLengths()
    {
        Run(Capture + """
            class C { @capture f; }
            var access = contexts['field'].access;
            ['get', 'set', 'has'].map(function (n) { return JSON.stringify(access[n].name) + access[n].length; }).join()
                + ' ' + JSON.stringify(Object.getOwnPropertyDescriptor(contexts['field'], 'access'));
            """).Should().Be("\"\"1,\"\"2,\"has\"1 {\"value\":{},\"writable\":true,\"enumerable\":true,\"configurable\":true}");
    }

    [Test]
    public void AddInitializerThrowsOnceItsDecoratorHasReturned()
    {
        Run("""
            var saved;
            function keep(value, context) { saved = context.addInitializer; }
            @keep class C {}
            class D { @keep m() {} }
            var result;
            try {
                saved(function () {});
                result = 'no throw';
            } catch (e) {
                result = e.constructor.name;
            }
            result;
            """).Should().Be("TypeError");
    }
}
