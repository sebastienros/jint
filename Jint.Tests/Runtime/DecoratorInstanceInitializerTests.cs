#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// When the initializers a decorator adds with <c>context.addInitializer</c> run as an instance is constructed, against
/// the decorators spec text, https://github.com/tc39/ecma262/pull/2417 (sebastienros/jint#4421).
/// <para>
/// <see href="https://tc39.es/ecma262/#sec-initializeinstanceelements">InitializeInstanceElements</see> adds the private
/// methods, then runs the extra initializers of the instance methods', getters' and setters' decorators, then
/// initializes each field and accessor in source order. InitializeFieldOrAccessor runs the extra initializers of a
/// field's or an accessor's decorators right after it defines that element. A base class does all of this before its
/// constructor body runs, a derived class when <c>super()</c> returns.
/// </para>
/// <para>
/// Node has no decorators, so the expected values follow the spec text and the test262 tests proposed for decorators
/// (https://github.com/tc39/test262/pull/5048). Each was also run through Babel's implementation of the spec text
/// (<c>@babel/plugin-proposal-decorators</c>, version <c>2023-11</c>), which agrees except where a test says otherwise.
/// </para>
/// </summary>
public class DecoratorInstanceInitializerTests
{
    /// <summary>
    /// <c>log(entry)</c> records an entry and answers it; <c>init(name)</c> is a decorator whose initializer logs
    /// <c>init name</c>.
    /// </summary>
    private const string Log = """
        var order = [];
        function log(entry) { order.push(entry); return entry; }
        function init(name) {
            return function (value, context) {
                context.addInitializer(function () { log('init ' + name); });
            };
        }
        """;

    private static JsValue Evaluate(string script) => new Engine().Evaluate(Log + "\n" + script);

    private static string Run(string script) => Evaluate(script).AsString();

    // ------------------------------------------------------------------ the methods' initializers

    /// <summary>
    /// The example of sebastienros/jint#4421.
    /// </summary>
    [Test]
    public void AMethodsInitializerRunsBeforeTheFields()
    {
        Run("""
            class C { @init('m') m() {} x = log('x'); }
            var c = new C();
            order.join() + ' ' + Object.hasOwn(c, 'undefined');
            """).Should().Be("init m,x false");
    }

    [Test]
    public void TheMethodsGettersAndSettersInitializersRunBeforeEveryFieldInSourceOrder()
    {
        Run("""
            class C {
                a = log('a');
                @init('m') m() {}
                @init('get g') get g() { return 1; }
                @init('set s') set s(v) {}
                @init('#p') #p() {}
                b = log('b');
            }
            new C();
            order.join();
            """).Should().Be("init m,init get g,init set s,init #p,a,b");
    }

    /// <summary>
    /// InitializePrivateMethods runs first, so a private method is on the instance when the methods' initializers run,
    /// and a private field is not yet. Babel answers <c>#m false</c>: its <c>access.has</c> for every private element
    /// of a class tests one brand, the class's first private field (<c>#f in o</c> here), which is not defined yet.
    /// </summary>
    [Test]
    public void ThePrivateMethodsAreAddedBeforeTheMethodsInitializersRun()
    {
        Run("""
            var field;
            function grab(value, context) { field = context; }
            function check(value, context) {
                context.addInitializer(function () {
                    log(context.name + ' ' + context.access.has(this) + ', #f ' + field.access.has(this));
                });
            }
            class C { @grab #f = 1; @check #m() {} }
            new C();
            order.join();
            """).Should().Be("#m true, #f false");
    }

    // ------------------------------------------------------------------ a field's and an accessor's initializers

    [Test]
    public void AFieldsInitializerRunsRightAfterThatField()
    {
        Run("""
            function after(value, context) {
                context.addInitializer(function () {
                    log('init ' + context.name + ' ' + Object.hasOwn(this, 'x') + ' ' + Object.hasOwn(this, 'y'));
                });
            }
            class C { @after x = log('x'); y = log('y'); }
            new C();
            order.join();
            """).Should().Be("x,init x true false,y");
    }

    [Test]
    public void APrivateFieldsInitializerRunsRightAfterThatField()
    {
        Run("""
            function after(value, context) {
                context.addInitializer(function () {
                    log('init ' + context.name + ' ' + context.access.has(this) + ' ' + Object.hasOwn(this, 'y'));
                });
            }
            class C { @after #x = log('#x'); y = log('y'); }
            new C();
            order.join();
            """).Should().Be("#x,init #x true false,y");
    }

    /// <summary>
    /// An auto-accessor's initializers are its own [[ExtraInitializers]], not the methods' list, so they run once its
    /// storage is initialized, with its value readable through the accessor.
    /// </summary>
    [Test]
    public void AnAccessorsInitializerRunsRightAfterItsStorageIsInitialized()
    {
        Run("""
            function after(value, context) {
                context.addInitializer(function () { log('init ' + context.name + ' ' + context.access.get(this)); });
            }
            class C {
                @after accessor a = log('a');
                b = log('b');
                @after accessor #c = log('#c');
                d = log('d');
            }
            new C();
            order.join();
            """).Should().Be("a,init a a,b,#c,init #c #c,d");
    }

    // ------------------------------------------------------------------ several decorators

    /// <summary>
    /// tc39/test262#5048's decorator-order-application-field-and-accessor and
    /// decorator-order-application-method-and-class, on an instance element with a field after it: the decorators
    /// are applied last to first, a field's or accessor's initializers run outermost first, and the extra
    /// initializers run in the order the decorators added them.
    /// </summary>
    [TestCase("@pushOrd(1, 3, 6) @pushOrd(0, 4, 5) x = ord.push(2); y = ord.push(7);", "0,1,2,3,4,5,6,7")]
    [TestCase("@pushOrd(1, 3, 6) @pushOrd(0, 4, 5) accessor x = ord.push(2); y = ord.push(7);", "0,1,2,3,4,5,6,7")]
    [TestCase("@pushOrd(1, -1, 3) @pushOrd(0, -1, 2) m() {} y = ord.push(4);", "0,1,2,3,4")]
    public void SeveralDecoratorsInitializersRunInTheOrderTheyWereAdded(string elements, string expected)
    {
        Run($$"""
            var ord = [];
            function pushOrd(applyOrd, initOrd, extraInitOrd) {
                return (value, context) => {
                    ord.push(applyOrd);
                    context.addInitializer(() => ord.push(extraInitOrd));
                    if (context.kind === 'field') {
                        return () => ord.push(initOrd);
                    }

                    if (context.kind === 'accessor') {
                        return { init() { ord.push(initOrd); } };
                    }
                };
            }
            class C { {{elements}} }
            new C();
            ord.join();
            """).Should().Be(expected);
    }

    /// <summary>
    /// tc39/test262#5048's decorator-order-phases-4-init-instance, with the class-with-super-class template it is
    /// generated from.
    /// </summary>
    [Test]
    public void TheInstanceIsInitializedInTheOrderTheProposedTest262TestsGive()
    {
        Run("""
            var ord = [];
            function pushOrd(initOrd, extraInitOrd) {
                return (value, context) => {
                    if (context.kind === 'field') {
                        context.addInitializer(() => ord.push(extraInitOrd));
                        return () => ord.push(initOrd);
                    } else if (context.kind === 'accessor') {
                        context.addInitializer(() => ord.push(extraInitOrd));
                        return { init() { ord.push(initOrd); } };
                    } else {
                        context.addInitializer(() => ord.push(initOrd));
                    }
                };
            }
            ord.push(0);
            class B {}
            class C extends B {
                constructor() {
                    ord.push(2);
                    super();
                    ord.push(13);
                }

                a = ord.push(5);
                @pushOrd(3) b() {}
                @pushOrd(7, 8) accessor c = ord.push(6);
                @pushOrd(4) get d() {}
                accessor f = ord.push(9);
                @pushOrd(11, 12) e = ord.push(10);
            }
            ord.push(1);
            new C();
            ord.push(14);
            ord.join();
            """).Should().Be("0,1,2,3,4,5,6,7,8,9,10,11,12,13,14");
    }

    // ------------------------------------------------------------------ derived classes and class decorators

    /// <summary>
    /// A base class initializes its instance before its constructor body runs; a derived class when <c>super()</c>
    /// returns, before the rest of its constructor body.
    /// </summary>
    [Test]
    public void ADerivedClassRunsItsInitializersWhenSuperReturns()
    {
        Run("""
            class B {
                constructor() { log('B'); }
                @init('B.m') m() {}
                @init('B.x') x = log('B.x');
            }
            class D extends B {
                constructor() {
                    log('D before super');
                    super();
                    log('D after super');
                }

                @init('D.n') n() {}
                @init('D.y') y = log('D.y');
            }
            new D();
            order.join();
            """).Should().Be("D before super,init B.m,B.x,init B.x,B,init D.n,D.y,init D.y,D after super");
    }

    /// <summary>
    /// A class decorator's replacement constructs through the class it replaced, whose instance initializers run with
    /// the replacement's instance.
    /// </summary>
    [Test]
    public void AReplacedClassRunsItsInitializersWithTheReplacementsInstance()
    {
        Run("""
            var Replacement;
            function replace(value) { return Replacement = class extends value {}; }
            function self(value, context) {
                context.addInitializer(function () { log('init ' + (this instanceof Replacement)); });
            }
            @replace class C { @self m() {} x = log('x'); }
            var c = new C();
            order.join() + ' ' + Reflect.ownKeys(c).join();
            """).Should().Be("init true,x x");
    }

    // ------------------------------------------------------------------ what the instance is left with

    /// <summary>
    /// Jint queued the instance initializers through a field record without a key, which left an own property named
    /// <c>"undefined"</c> on every instance.
    /// </summary>
    [TestCase("@init('m') m() {}", "init m", "")]
    [TestCase("@init('m') m() {} x = 1;", "init m", "x")]
    [TestCase("@init('x') x = 1; #y = 2; @init('a') accessor a = 3;", "init x,init a", "x")]
    public void NoOwnUndefinedPropertyIsLeftOnTheInstance(string elements, string expectedOrder, string expectedKeys)
    {
        Run($$"""
            class C { {{elements}} }
            var c = new C();
            order.join() + '|' + Reflect.ownKeys(c).map(String).join();
            """).Should().Be(expectedOrder + "|" + expectedKeys);
    }

    /// <summary>
    /// A last field's initializer that freezes the instance leaves it frozen. Twenty constructions take the
    /// constructor past the point where its instances start out shaped.
    /// </summary>
    [Test]
    public void AnInitializerThatFreezesTheInstanceLeavesItFrozen()
    {
        Run("""
            function frozen(value, context) { context.addInitializer(function () { Object.freeze(this); }); }
            class C { x = 1; @frozen y = 2; }
            var ok = true;
            for (var i = 0; i < 20; i++) {
                var c = new C();
                ok = ok && Object.isFrozen(c) && c.x === 1 && c.y === 2;
            }
            ok + ' ' + Reflect.ownKeys(new C()).join();
            """).Should().Be("true x,y");
    }

    /// <summary>
    /// An initializer that throws ends the construction: the fields after it are never defined.
    /// </summary>
    [TestCase("a = log('a'); @fails m() {} b = log('b');", "init m")]
    [TestCase("@fails a = log('a'); b = log('b');", "a,init a")]
    public void AnInitializerThatThrowsEndsTheConstruction(string elements, string expected)
    {
        Run($$"""
            function fails(value, context) {
                context.addInitializer(function () { throw new Error('init ' + context.name); });
            }
            class C { {{elements}} }
            try { new C(); } catch (e) { log(e.message); }
            order.join();
            """).Should().Be(expected);
    }

    [Test]
    public void EveryConstructionRunsTheInitializersWithItsOwnInstance()
    {
        Run("""
            var instances = [];
            function collect(value, context) { context.addInitializer(function () { instances.push(this); }); }
            class C { @collect m() {} @collect x = 1; }
            var first = new C();
            var second = new C();
            [instances.length, instances[0] === first, instances[1] === first, instances[2] === second, instances[3] === second].join();
            """).Should().Be("4,true,true,true,true");
    }

    /// <summary>
    /// A decorated class whose decorators add no instance initializer keeps the lists an undecorated class keeps, and
    /// still defines its fields when it has no private method.
    /// </summary>
    [TestCase("class C { @twice x = 2; y = 3; }", "x,y 4 3")]
    [TestCase("@(c => c) class C { x = 2; y = 3; }", "x,y 2 3")]
    [TestCase("class C { @init('s') static s() {} x = 2; y = 3; }", "x,y 2 3")]
    public void ADecoratedClassWithoutInstanceInitializersDefinesItsFields(string declaration, string expected)
    {
        Run($$"""
            function twice(value, context) { return function (v) { return v * 2; }; }
            {{declaration}}
            var c = new C();
            Reflect.ownKeys(c).join() + ' ' + c.x + ' ' + c.y;
            """).Should().Be(expected);
    }
}
