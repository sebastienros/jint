#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A class whose heritage or computed key suspends the frame it is evaluated in resumes where it suspended.
/// <para>
/// <see href="https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation">ClassDefinitionEvaluation</see>
/// evaluates the heritage with the class scope as the running LexicalEnvironment (step 8), then the elements one by
/// one with the class scope and the class's PrivateEnvironment (steps 12-13), and only then is the class binding
/// initialized — by ClassDefinitionEvaluation for the inner one and by
/// <see href="https://tc39.es/ecma262/#sec-runtime-semantics-bindingclassdeclarationevaluation">BindingClassDeclarationEvaluation</see>
/// for a declaration's. An <c>await</c> or a <c>yield</c> anywhere in the heritage or in a computed key suspends the
/// frame in the middle of that, and resuming it continues there: nothing evaluated before the suspension point runs
/// again, the scope a closure captured there is the one the class binding is initialized in, and the binding stays
/// uninitialized until the class is done.
/// </para>
/// <para>
/// Jint replays a suspended body, and the class definition did not take part in the replay
/// (sebastienros/jint#4395). A suspended heritage was checked for being a constructor before the frame was checked
/// for having suspended, so the placeholder raised a TypeError inside a suspended frame and an async body replayed
/// from its first statement; a generator threw it out of <c>next()</c>. A suspended computed key unwound, but the
/// class declaration initialized its binding with the placeholder on the way out, and the resume rebuilt the class
/// from scratch: the heritage and every key before the suspended one ran again, in an environment and with a
/// strictness an async function had captured from inside the class. Each expected value is Node 26's, except the
/// decorator tests, which follow the decorators proposal (Node has no decorators).
/// </para>
/// </summary>
public class SuspendedClassDefinitionTests
{
    private const string Setup = """
        var log = [];
        var THROW = { throwInto: true };
        class Base { m() { return 'base'; } }
        """;

    /// <summary>
    /// The frames a class can suspend, each with the keyword that suspends it. <c>SUSPEND</c> in a body becomes that
    /// keyword, and <c>REJECT</c> a suspension that resumes with a throw of <c>Error('no')</c>.
    /// </summary>
    public static readonly string[] Suspensions = ["async function", "generator", "async generator await", "async generator yield"];

    private static string Probe(string suspension, string body, string report)
    {
        return suspension switch
        {
            "async function" => AsyncProbe(Substitute(body, "await"), report),
            "generator" => GeneratorProbe(Substitute(body, "yield"), report),
            "async generator await" => AsyncGeneratorProbe(Substitute(body, "await"), report),
            "async generator yield" => AsyncGeneratorProbe(Substitute(body, "yield"), report),
            _ => throw new ArgumentOutOfRangeException(nameof(suspension)),
        };

        static string Substitute(string body, string keyword) => body
            .Replace("REJECT", keyword == "await" ? "await Promise.reject(new Error('no'))" : "yield THROW")
            .Replace("SUSPEND", keyword);
    }

    /// <summary>
    /// Runs <paramref name="body"/> inside an async function bracketed by the <c>pre</c>/<c>post</c> markers and
    /// returns <c>"&lt;report&gt;|&lt;log&gt;"</c>, or <c>"threw &lt;message&gt;|&lt;log&gt;"</c> when it rejects.
    /// </summary>
    private static string AsyncProbe(string body, string report) => new Engine().Evaluate($$"""
        {{Setup}}
        async function m() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        m().then(
          function (r) { return String(r) + '|' + log.join(','); },
          function (e) { return 'threw ' + e.message + '|' + log.join(','); });
        """).UnwrapIfPromise().AsString();

    /// <summary>
    /// The synchronous-generator twin of <see cref="AsyncProbe"/>: every <c>yield</c> is resumed with its own
    /// yielded value, standing in for the awaited one, and a yielded <c>THROW</c> with a throw of <c>Error('no')</c>.
    /// </summary>
    private static string GeneratorProbe(string body, string report) => new Engine().Evaluate($$"""
        {{Setup}}
        function* g() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        (function () {
          try {
            var it = g();
            var res = it.next();
            for (var i = 0; !res.done && i < 8; i++) res = res.value === THROW ? it.throw(new Error('no')) : it.next(res.value);
            return String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
          } catch (e) {
            return 'threw ' + e.message + '|' + log.join(',');
          }
        })();
        """).AsString();

    /// <summary>
    /// The async-generator twin: <c>await</c>s and <c>yield</c>s both suspend it, and the consumer drives it with
    /// <c>next()</c> until it is done. Both generator drivers stop after a fixed number of steps, so a generator that
    /// never finishes fails the assertion instead of spinning the test host.
    /// </summary>
    private static string AsyncGeneratorProbe(string body, string report) => new Engine().Evaluate($$"""
        {{Setup}}
        async function* ag() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        async function drive() {
          try {
            var it = ag();
            var res = await it.next();
            for (var i = 0; !res.done && i < 8; i++) res = await (res.value === THROW ? it.throw(new Error('no')) : it.next(res.value));
            return String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
          } catch (e) {
            return 'threw ' + e.message + '|' + log.join(',');
          }
        }
        drive();
        """).UnwrapIfPromise().AsString();

    // ------------------------------------------------------------------ a suspension in the heritage

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageRunsWhatPrecedesItOnce(string suspension)
    {
        Probe(suspension, "class C extends (log.push('h'), SUSPEND Base) {}", "Object.getPrototypeOf(C) === Base && new C().m()")
            .Should().Be("base|pre,h,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageInitializesTheBindingEveryClosureReads(string suspension)
    {
        Probe(suspension, "let read = () => C; let getC; class C extends (getC = () => C, SUSPEND Base) {}", "(getC() === C) + ',' + (read() === C)")
            .Should().Be("true,true|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageThatResumesWithAThrowLeavesTheClassScopeFirst(string suspension)
    {
        Probe(suspension,
                "let K = 'outer'; try { (class K extends (log.push('h'), REJECT) {}); log.push('not here'); } catch (e) { log.push('caught ' + e.message + ' ' + K); }",
                "'r'")
            .Should().Be("r|pre,h,caught no outer,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageThatResumesWithAnUncaughtThrowEndsTheFrameOnce(string suspension)
    {
        Probe(suspension, "class C extends (log.push('h'), REJECT) {} log.push('not here');", "'r'")
            .Should().Be("threw no|pre,h");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageThatIsNotAConstructorThrowsOnce(string suspension)
    {
        Probe(suspension, "try { class C extends (log.push('h'), SUSPEND 1) {} } catch (e) { log.push(e.constructor.name); }", "'r'")
            .Should().Be("r|pre,h,TypeError,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageOfANestedClassResumesBothClasses(string suspension)
    {
        Probe(suspension,
                "class A extends (log.push('outer'), class B extends (log.push('inner'), SUSPEND Base) {}) {}",
                "new A().m() + ',' + Object.getPrototypeOf(A).name")
            .Should().Be("base,B|pre,outer,inner,post");
    }

    /// <summary>
    /// The reference each of these writes the class to is resolved before the class is evaluated, so a resume that
    /// starts inside the class scope resolves it there: to the class's own inner binding, still uninitialized.
    /// </summary>
    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageOfAClassExpressionIsBoundWhereItsReferenceResolves(string suspension)
    {
        Probe(suspension,
                "const D = class extends (SUSPEND Base) {}; let K = class K extends (SUSPEND Base) {}; var V; V = class V extends (SUSPEND Base) {}; var o = {}; o.w = class W extends (SUSPEND Base) {};",
                "[D.name, K.name, typeof K, V.name, o.w.name].join(',')")
            .Should().Be("D,K,function,V,W|pre,post");
    }

    /// <summary>
    /// A class is strict mode code, and the frame around it is not: the probe's function is sloppy, so an
    /// assignment to an undeclared name after the class creates a global rather than throwing.
    /// </summary>
    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageLeavesTheFrameAsStrictAsItWas(string suspension)
    {
        Probe(suspension, "class C extends (SUSPEND Base) {} undeclaredAfterHeritage = 1;", "typeof undeclaredAfterHeritage")
            .Should().Be("number|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageInALoopResumesEachIteration(string suspension)
    {
        Probe(suspension,
                "for (let i = 0; i < 2; i++) { class C extends (log.push('h' + i), SUSPEND Base) {} log.push(Object.getPrototypeOf(C) === Base); }",
                "'r'")
            .Should().Be("r|pre,h0,true,h1,true,post");
    }

    /// <summary>
    /// The decorators proposal evaluates a class's decorator expressions before its heritage
    /// (https://tc39.es/proposal-decorators/), so a resume into the heritage must not evaluate them again.
    /// </summary>
    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedHeritageDoesNotEvaluateTheDecoratorsAgain(string suspension)
    {
        Probe(suspension,
                "function dec() { log.push('apply'); } @(log.push('d'), dec) class C extends (SUSPEND Base) {}",
                "Object.getPrototypeOf(C) === Base")
            .Should().Be("true|pre,d,apply,post");
    }

    // ------------------------------------------------------------------ a suspension in a decorator

    // A decorator's expression is evaluated in the frame around the class too, before anything of the class exists
    // (https://tc39.es/proposal-decorators/). Its suspension stops the class there, and the resume evaluates the class
    // from the start: the class is defined once, each decorator applied once and each static block run once. The
    // expected values follow the proposal, since Node has no decorators.

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedClassDecoratorDecoratesTheClassOnce(string suspension)
    {
        Probe(suspension,
                "let applied = 0, staticRuns = 0; function dec(c) { applied++; } @(SUSPEND dec) class C { m() {} static { staticRuns++; } }",
                "applied + ',' + staticRuns + ',' + typeof C.prototype.m")
            .Should().Be("1,1,function|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedMethodDecoratorDecoratesTheMethodOnce(string suspension)
    {
        Probe(suspension,
                "let applied = 0, staticRuns = 0; function mdec(m) { applied++; return function () { return m.call(this) + 1; }; } class C { @(SUSPEND mdec) m() { return 1; } static { staticRuns++; } }",
                "applied + ',' + staticRuns + ',' + new C().m()")
            .Should().Be("1,1,2|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedFieldDecoratorDecoratesTheFieldOnce(string suspension)
    {
        Probe(suspension,
                "let applied = 0, staticRuns = 0; function fdec() { applied++; return v => v + 1; } class C { @(SUSPEND fdec) x = 1; static { staticRuns++; } }",
                "applied + ',' + staticRuns + ',' + new C().x")
            .Should().Be("1,1,2|pre,post");
    }

    /// <summary>
    /// The decorators after the one that suspended are not evaluated until the resume reaches them.
    /// </summary>
    [TestCaseSource(nameof(Suspensions))]
    public void ADecoratorAfterASuspendedOneIsEvaluatedOnce(string suspension)
    {
        Probe(suspension,
                "let cRuns = 0; function a() {} function b() {} function c() {} @(SUSPEND a) @(cRuns++, b) class C { @(cRuns++, c) m() {} }",
                "cRuns")
            .Should().Be("2|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedDecoratorAndASuspendedHeritageEachResumeOnce(string suspension)
    {
        Probe(suspension,
                "let applied = 0; function dec(c) { applied++; } @(SUSPEND dec) class C extends (log.push('h'), SUSPEND Base) {}",
                "applied + ',' + (Object.getPrototypeOf(C) === Base)")
            .Should().Be("1,true|pre,h,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedDecoratorLeavesTheFrameAsStrictAsItWas(string suspension)
    {
        Probe(suspension, "function dec() {} @(SUSPEND dec) class C {} undeclaredAfterDecorator = 1;", "typeof undeclaredAfterDecorator")
            .Should().Be("number|pre,post");
    }

    // ------------------------------------------------------------------ a suspension in a computed key

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedComputedKeyRunsWhatPrecedesItOnce(string suspension)
    {
        Probe(suspension,
                "class C extends (log.push('h'), Base) { [(log.push('k1'), 'a')]() {} [(log.push('k2'), SUSPEND 'b')]() {} static [(log.push('k3'), 'c')] = 1; static { log.push('static'); } }",
                "Object.getOwnPropertyNames(C.prototype).join('|') + ' ' + C.c")
            .Should().Be("constructor|a|b 1|pre,h,k1,k2,k3,static,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedComputedKeyInitializesTheBindingEveryClosureReads(string suspension)
    {
        Probe(suspension,
                "let read = () => C; let getC; class C { [(getC = () => C, SUSPEND 'k')]() { return 1; } }",
                "(getC() === C) + ',' + (read() === C) + ',' + new C().k()")
            .Should().Be("true,true,1|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedComputedKeyKeepsTheClassPrivateNames(string suspension)
    {
        Probe(suspension,
                "class C { #p() { return 'p'; } static #s = 's'; [SUSPEND 'k']() { return this.#p(); } static s() { return C.#s; } }",
                "new C().k() + C.s()")
            .Should().Be("ps|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void AClassWhoseHeritageAndComputedKeySuspendResumesTwice(string suspension)
    {
        Probe(suspension,
                "class C extends (log.push('h'), SUSPEND Base) { [(log.push('k'), SUSPEND 'b')]() { return 'b'; } }",
                "new C().b() + new C().m()")
            .Should().Be("bbase|pre,h,k,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedComputedKeyThatResumesWithAThrowLeavesTheClassScopeFirst(string suspension)
    {
        Probe(suspension,
                "let K = 'outer'; try { (class K { [(log.push('k'), REJECT)]() {} }); log.push('not here'); } catch (e) { log.push('caught ' + e.message + ' ' + K); }",
                "'r'")
            .Should().Be("r|pre,k,caught no outer,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedComputedKeyLeavesTheFrameAsStrictAsItWas(string suspension)
    {
        Probe(suspension, "class C { [SUSPEND 'k']() {} } undeclaredAfterKey = 1;", "typeof undeclaredAfterKey")
            .Should().Be("number|pre,post");
    }

    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedComputedKeyDoesNotEvaluateTheDecoratorsAgain(string suspension)
    {
        // `static` keeps the computed key from reading as a member access on the decorator expression.
        Probe(suspension,
                "function mdec() { log.push('apply'); } class C { @(log.push('d'), mdec) static [SUSPEND 'k']() { return 1; } }",
                "C.k()")
            .Should().Be("1|pre,d,apply,post");
    }

    /// <summary>
    /// An accessor or a generator method whose computed key suspends defines nothing until the key is known. The
    /// helper that defines one is shared with object literals, and it saw a generator's suspension only, so an
    /// await went on to define the member under the key <c>"undefined"</c> on a target the resume reuses.
    /// </summary>
    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedAccessorOrGeneratorKeyDefinesOnlyItsOwnKey(string suspension)
    {
        Probe(suspension,
                "const o = { get [SUSPEND 'k']() { return 1; }, *[SUSPEND 'g']() {}, set [SUSPEND 's'](v) {} }; class C { get [SUSPEND 'k']() { return 1; } static *[SUSPEND 'g']() {} static set [SUSPEND 's'](v) {} }",
                "Object.getOwnPropertyNames(o).join('|') + ' ' + Object.getOwnPropertyNames(C.prototype).join('|') + ' ' + Object.getOwnPropertyNames(C).join('|')")
            .Should().Be("k|g|s constructor|k length|name|prototype|g|s|pre,post");
    }

    /// <summary>
    /// The decorators proposal's auto-accessor (https://tc39.es/proposal-decorators/) defines a getter and a setter
    /// under its key, the same way.
    /// </summary>
    [TestCaseSource(nameof(Suspensions))]
    public void ASuspendedAutoAccessorKeyDefinesOnlyItsOwnKey(string suspension)
    {
        Probe(suspension,
                "class C { accessor [SUSPEND 'a'] = 1; static accessor [SUSPEND 'b'] = 2; }",
                "Object.getOwnPropertyNames(C.prototype).join('|') + ' ' + new C().a + C.b")
            .Should().Be("constructor|a 12|pre,post");
    }

    // ------------------------------------------------------------------ the binding, seen from outside the frame

    /// <summary>
    /// While the frame is suspended inside the class, the class binding is still in its temporal dead zone: it is
    /// initialized only once the class has been defined.
    /// </summary>
    [TestCase("class C extends (SUSPEND Base) {}")]
    [TestCase("class C { [SUSPEND 'k']() {} }")]
    [TestCase("@(SUSPEND (c => c)) class C {}")]
    public void TheClassBindingIsUninitializedWhileTheFrameIsSuspended(string declaration)
    {
        const string Peek = "peek = function () { try { C; return 'initialized'; } catch (e) { return e.constructor.name; } };";

        var async = new Engine().Evaluate($$"""
            {{Setup}}
            var peek;
            async function f() { {{Peek}} {{declaration.Replace("SUSPEND", "await")}} return peek(); }
            var p = f();
            var during = peek();
            p.then(function (r) { return during + ',' + r; });
            """).UnwrapIfPromise().AsString();

        var generator = new Engine().Evaluate($$"""
            {{Setup}}
            var peek;
            function* g() { {{Peek}} {{declaration.Replace("SUSPEND", "yield")}} return peek(); }
            var it = g();
            var first = it.next();
            var during = peek();
            during + ',' + it.next(first.value).value;
            """).AsString();

        async.Should().Be("ReferenceError,initialized");
        generator.Should().Be("ReferenceError,initialized");
    }

    // ------------------------------------------------------------------ controls

    /// <summary>
    /// A class that does not suspend, in a frame that suspends after it, is evaluated once — the shape the build-time
    /// gate keeps on the unchanged path.
    /// </summary>
    [TestCaseSource(nameof(Suspensions))]
    public void AClassThatDoesNotSuspendIsEvaluatedOnceControl(string suspension)
    {
        Probe(suspension, "class C extends (log.push('h'), Base) { [(log.push('k'), 'a')]() {} } SUSPEND 0;", "new C().a === C.prototype.a")
            .Should().Be("true|pre,h,k,post");
    }

    /// <summary>
    /// A heritage that throws leaves the class scope before the throw propagates (ClassDefinitionEvaluation step 8.d
    /// restores the LexicalEnvironment before step 8.e's <c>?</c>), so a <c>catch</c> outside the class reads the
    /// enclosing binding of the class's name, not the class's own uninitialized one. No frame suspends here.
    /// </summary>
    [Test]
    public void AHeritageThatThrowsLeavesTheClassScope()
    {
        new Engine().Evaluate("""
            var log = [];
            let K = 'outer';
            try { (class K extends (() => { throw new Error('no'); })() {}); } catch (e) { log.push('caught ' + e.message + ' ' + K); }
            log.join(',');
            """).AsString().Should().Be("caught no outer");
    }
}
