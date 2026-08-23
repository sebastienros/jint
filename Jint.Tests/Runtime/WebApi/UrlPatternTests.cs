#if NET8_0_OR_GREATER
#nullable enable

using System.Text.RegularExpressions;
using Jint.Runtime;
using Jint.WebApi.Url.Pattern;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>URLPattern</c> class as the URL Pattern Standard specifies it —
/// https://urlpattern.spec.whatwg.org/#urlpattern-class.
/// </summary>
/// <remarks>
/// The pattern syntax, the constructor-string shorthand and the match results are covered exhaustively by
/// <see cref="UrlPatternCorpusTests"/>, which runs the Web Platform Tests corpus through the same script surface.
/// What is tested here is what no corpus row reaches: the WebIDL skin and its overload resolution, the property
/// attributes, the brand checks, the shape of the object the two operations hand back, and the two properties
/// that follow from building on the engine's own <c>RegExp</c> — that a patched <c>RegExp.prototype.exec</c>
/// cannot be observed, and that a hostile author regexp ends in the engine's regex timeout rather than a hang.
/// </remarks>
public class UrlPatternTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Url));

    [Fact]
    public void IsInstalledOnlyWithTheUrlFeature()
    {
        new Engine().Evaluate("typeof URLPattern").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof URLPattern").AsString().Should().Be("undefined");

        WebEngine().Evaluate("typeof URLPattern").AsString().Should().Be("function");

        // It rides the URL flag rather than one of its own, so asking for URL is what brings it.
        new Engine(options => options.UseWebApis()).Evaluate("typeof URLPattern").AsString().Should().Be("function");
    }

    [Fact]
    public void IsAWebIdlInterfaceObject()
    {
        var engine = WebEngine();

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'URLPattern')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeFalse();

        engine.Evaluate("URLPattern.name").AsString().Should().Be("URLPattern");

        // The smallest number of required arguments across the two constructor overloads is zero.
        engine.Evaluate("URLPattern.length").AsNumber().Should().Be(0);

        engine.Evaluate("Object.getPrototypeOf(URLPattern) === Function.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("URLPattern.prototype[Symbol.toStringTag]").AsString().Should().Be("URLPattern");
        engine.Evaluate("URLPattern.prototype.constructor === URLPattern").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(new URLPattern({}))").AsString().Should().Be("[object URLPattern]");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URLPattern({})"))
            .Error.ToString().Should().Contain("TypeError");
    }

    [Fact]
    public void EveryAttributeIsAReadOnlyAccessorOnThePrototype()
    {
        var engine = WebEngine();

        foreach (var name in new[] { "protocol", "username", "password", "hostname", "port", "pathname", "search", "hash", "hasRegExpGroups" })
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(URLPattern.prototype, '{name}')").AsObject();

            descriptor.Get("configurable").AsBoolean().Should().BeTrue($"{name} is a WebIDL attribute");
            descriptor.Get("enumerable").AsBoolean().Should().BeTrue($"{name} is a WebIDL attribute");
            descriptor.Get("set").IsUndefined().Should().BeTrue($"{name} is readonly");
            descriptor.Get("get").IsUndefined().Should().BeFalse($"{name} is an accessor");
        }

        // An instance therefore has no own property of its own.
        engine.Evaluate("Object.getOwnPropertyNames(new URLPattern({})).length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void ParsesTheStandardsIntroductoryConstructorString()
    {
        // https://urlpattern.spec.whatwg.org/#example-intro
        var engine = WebEngine();
        engine.Execute("const p = new URLPattern('https://example.com/:category/*');");

        engine.Evaluate("p.protocol").AsString().Should().Be("https");
        engine.Evaluate("p.username").AsString().Should().Be("*");
        engine.Evaluate("p.password").AsString().Should().Be("*");
        engine.Evaluate("p.hostname").AsString().Should().Be("example.com");
        engine.Evaluate("p.port").AsString().Should().Be("");
        engine.Evaluate("p.pathname").AsString().Should().Be("/:category/*");
        engine.Evaluate("p.search").AsString().Should().Be("*");
        engine.Evaluate("p.hash").AsString().Should().Be("*");

        engine.Evaluate("p.test('https://example.com/products/')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.test('https://example.com/blog/our-greatest-product-ever')").AsBoolean().Should().BeTrue();

        engine.Evaluate("p.test('https://example.com/')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.test('http://example.com/products/')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.test('https://example.com:8443/blog/our-greatest-product-ever')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ParsesTheStandardsModifierAndRegexpConstructorString()
    {
        // https://urlpattern.spec.whatwg.org/#example-intro-2
        var engine = WebEngine();
        engine.Execute("const p = new URLPattern('http{s}?://{:subdomain.}?shop.example/products/:id([0-9]+)#reviews');");

        engine.Evaluate("p.protocol").AsString().Should().Be("http{s}?");
        engine.Evaluate("p.hostname").AsString().Should().Be("{:subdomain.}?shop.example");
        engine.Evaluate("p.pathname").AsString().Should().Be("/products/:id([0-9]+)");
        engine.Evaluate("p.search").AsString().Should().Be("");
        engine.Evaluate("p.hash").AsString().Should().Be("reviews");

        engine.Evaluate("p.test('https://shop.example/products/74205#reviews')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.test('https://kathryn@voyager.shop.example/products/74656#reviews')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.test('http://insecure.shop.example/products/1701#reviews')").AsBoolean().Should().BeTrue();

        engine.Evaluate("p.test('https://shop.example/products/2000')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.test('http://shop.example:8080/products/0#reviews')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.test('https://nx.shop.example/products/01?speed=5#reviews')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.test('https://shop.example/products/chair#reviews')").AsBoolean().Should().BeFalse();

        engine.Evaluate("p.exec('https://kathryn@voyager.shop.example/products/74656#reviews').hostname.groups.subdomain")
            .AsString().Should().Be("voyager");
    }

    [Fact]
    public void ResolvesARelativeConstructorStringAgainstItsBaseUrl()
    {
        // https://urlpattern.spec.whatwg.org/#example-intro-3
        var engine = WebEngine();
        engine.Execute("const p = new URLPattern('../admin/*', 'https://discussion.example/forum/?page=2');");

        engine.Evaluate("p.protocol").AsString().Should().Be("https");
        engine.Evaluate("p.hostname").AsString().Should().Be("discussion.example");
        engine.Evaluate("p.port").AsString().Should().Be("");
        engine.Evaluate("p.pathname").AsString().Should().Be("/admin/*");
        engine.Evaluate("p.search").AsString().Should().Be("*");
        engine.Evaluate("p.hash").AsString().Should().Be("*");

        engine.Evaluate("p.test('https://discussion.example/admin/')").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.test('https://edd:librarian@discussion.example/admin/update?id=1')").AsBoolean().Should().BeTrue();

        engine.Evaluate("p.test('https://discussion.example/forum/admin/')").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.test('http://discussion.example:8080/admin/update?id=1')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ResolvesTheTwoArgumentOverloadByTheSecondArgumentsType()
    {
        var engine = WebEngine();

        // An object at the distinguishing position selects the options overload, so this is not a base URL.
        engine.Execute("const opts = new URLPattern({ pathname: '/FOO' }, { ignoreCase: true });");
        engine.Evaluate("opts.test({ pathname: '/foo' })").AsBoolean().Should().BeTrue();

        // undefined and null go the same way, because the overload with a dictionary at that position wins.
        engine.Evaluate("new URLPattern({ pathname: '/foo' }, undefined).pathname").AsString().Should().Be("/foo");
        engine.Evaluate("new URLPattern({ pathname: '/foo' }, null).pathname").AsString().Should().Be("/foo");

        // Anything else is a USVString base URL.
        engine.Evaluate("new URLPattern('/foo', 'https://example.com').hostname").AsString().Should().Be("example.com");

        // A third argument forces the base URL overload whatever the second argument looks like.
        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("new URLPattern('/foo', { ignoreCase: true }, 'https://example.com')"));
    }

    [Fact]
    public void IgnoreCaseReachesOnlyThePathnameSearchAndHash()
    {
        var engine = WebEngine();

        engine.Evaluate("new URLPattern({ pathname: '/FOO' }).test({ pathname: '/foo' })").AsBoolean().Should().BeFalse();
        engine.Evaluate("new URLPattern({ pathname: '/FOO' }, { ignoreCase: true }).test({ pathname: '/foo' })").AsBoolean().Should().BeTrue();
        engine.Evaluate("new URLPattern({ search: 'A=B' }, { ignoreCase: true }).test({ search: 'a=b' })").AsBoolean().Should().BeTrue();
        engine.Evaluate("new URLPattern({ hash: 'FRAG' }, { ignoreCase: true }).test({ hash: 'frag' })").AsBoolean().Should().BeTrue();

        // The credentials are not among the components the flag is threaded into: the specification builds the
        // pathname, search and hash components from a copy of the options carrying ignore case, and leaves the
        // protocol, username, password, hostname and port components on the plain default and hostname options.
        engine.Evaluate("new URLPattern({ username: 'ABC' }, { ignoreCase: true }).test({ username: 'abc' })").AsBoolean().Should().BeFalse();
        engine.Evaluate("new URLPattern({ password: 'ABC' }, { ignoreCase: true }).test({ password: 'abc' })").AsBoolean().Should().BeFalse();

        // A non-object options argument is a TypeError; undefined and null give the member its default.
        engine.Evaluate("new URLPattern({ pathname: '/FOO' }, { }).test({ pathname: '/foo' })").AsBoolean().Should().BeFalse();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern({ pathname: '/f' }, 1, 2)"));
    }

    [Fact]
    public void HasRegExpGroupsReportsOnlyCustomRegularExpressions()
    {
        var engine = WebEngine();

        engine.Evaluate("new URLPattern({ pathname: '/foo' }).hasRegExpGroups").AsBoolean().Should().BeFalse();
        engine.Evaluate("new URLPattern({ pathname: '/:id' }).hasRegExpGroups").AsBoolean().Should().BeFalse();
        engine.Evaluate("new URLPattern({ pathname: '/*' }).hasRegExpGroups").AsBoolean().Should().BeFalse();
        engine.Evaluate("new URLPattern({ pathname: '/:id([0-9]+)' }).hasRegExpGroups").AsBoolean().Should().BeTrue();

        // Any component counts, not only the pathname.
        engine.Evaluate("new URLPattern({ hostname: '(sub|www).example.com' }).hasRegExpGroups").AsBoolean().Should().BeTrue();

        // A written-out regexp that is exactly the segment wildcard is folded back into a segment wildcard, so it
        // is not a regexp group at all.
        engine.Evaluate("new URLPattern({ pathname: '/:id([^\\\\/]+?)' }).hasRegExpGroups").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void HasRegExpGroupsAnswersForEveryComponent()
    {
        // A port of WPT's urlpattern/resources/urlpattern-hasregexpgroups-tests.js at the pinned commit, which is
        // a separate file from the corpus and so is not covered by UrlPatternCorpusTests.
        var engine = WebEngine();

        var failures = engine.Evaluate(
            """
            (function () {
              const problems = [];
              const check = (actual, expected, what) => {
                if (actual !== expected) problems.push(`${what}: expected ${expected} but was ${actual}`);
              };

              check('hasRegExpGroups' in URLPattern.prototype, true, 'hasRegExpGroups is implemented');
              check(new URLPattern({}).hasRegExpGroups, false, 'match-everything pattern');

              for (const c of ['protocol', 'username', 'password', 'hostname', 'port', 'pathname', 'search', 'hash']) {
                check(new URLPattern({ [c]: '*' }).hasRegExpGroups, false, `wildcard in ${c}`);
                check(new URLPattern({ [c]: ':foo' }).hasRegExpGroups, false, `segment wildcard in ${c}`);
                check(new URLPattern({ [c]: ':foo?' }).hasRegExpGroups, false, `optional segment wildcard in ${c}`);
                check(new URLPattern({ [c]: ':foo(hi)' }).hasRegExpGroups, true, `named regexp group in ${c}`);
                check(new URLPattern({ [c]: '(hi)' }).hasRegExpGroups, true, `anonymous regexp group in ${c}`);

                // The protocol and the port accept far less than the other six in any case.
                if (c !== 'protocol' && c !== 'port') {
                  check(new URLPattern({ [c]: 'a-{:hello}-z-*-a' }).hasRegExpGroups, false, `wildcards and fixed text in ${c}`);
                  check(new URLPattern({ [c]: 'a-(hi)-z-(lo)-a' }).hasRegExpGroups, true, `regexp groups and fixed text in ${c}`);
                }
              }

              check(new URLPattern({ pathname: '/a/:foo/:baz?/b/*' }).hasRegExpGroups, false, 'complex pathname with no regexp');
              check(new URLPattern({ pathname: '/a/:foo/:baz([a-z]+)?/b/*' }).hasRegExpGroups, true, 'complex pathname with regexp');

              return problems.join('; ');
            })();
            """).AsString();

        failures.Should().BeEmpty();
    }

    [Fact]
    public void ExecReturnsTheDictionaryTheStandardDeclares()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLPattern({ pathname: '/blog/:year(\\\\d+)/:slug' });");
        engine.Execute("const r = p.exec({ pathname: '/blog/2012/hello' });");

        // URLPatternResult's members, in declaration order.
        engine.Evaluate("Object.keys(r).join(',')").AsString()
            .Should().Be("inputs,protocol,username,password,hostname,port,pathname,search,hash");

        // URLPatternComponentResult's members, in declaration order.
        engine.Evaluate("Object.keys(r.pathname).join(',')").AsString().Should().Be("input,groups");

        engine.Evaluate("r.pathname.input").AsString().Should().Be("/blog/2012/hello");
        engine.Evaluate("r.pathname.groups.year").AsString().Should().Be("2012");
        engine.Evaluate("r.pathname.groups.slug").AsString().Should().Be("hello");
        engine.Evaluate("Object.keys(r.pathname.groups).join(',')").AsString().Should().Be("year,slug");

        // The groups record is an ordinary object, not a null-prototype one.
        engine.Evaluate("Object.getPrototypeOf(r.pathname.groups) === Object.prototype").AsBoolean().Should().BeTrue();

        // inputs echoes what was passed in, as a fresh dictionary carrying only the members that were present.
        engine.Evaluate("Array.isArray(r.inputs)").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.inputs.length").AsNumber().Should().Be(1);
        engine.Evaluate("Object.keys(r.inputs[0]).join(',')").AsString().Should().Be("pathname");

        // A string input with a base URL puts both in the list.
        engine.Execute("const r2 = new URLPattern({ pathname: '/a' }).exec('/a', 'https://example.com');");
        engine.Evaluate("r2.inputs.join('|')").AsString().Should().Be("/a|https://example.com");

        // A group that did not participate is present and undefined.
        engine.Execute("const r3 = new URLPattern({ pathname: '/a{/:b}?' }).exec({ pathname: '/a' });");
        engine.Evaluate("'b' in r3.pathname.groups").AsBoolean().Should().BeTrue();
        engine.Evaluate("r3.pathname.groups.b === undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void TestIsExecReducedToABoolean()
    {
        var engine = WebEngine();
        engine.Execute("const p = new URLPattern({ pathname: '/foo' });");

        engine.Evaluate("p.test({ pathname: '/foo' })").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.test({ pathname: '/bar' })").AsBoolean().Should().BeFalse();
        engine.Evaluate("p.exec({ pathname: '/bar' })").IsNull().Should().BeTrue();

        // An input that is not a URL at all is a non-match, not an error.
        engine.Evaluate("p.exec('not a url')").IsNull().Should().BeTrue();
        engine.Evaluate("p.test('not a url')").AsBoolean().Should().BeFalse();

        // Both default their input to an empty dictionary.
        engine.Evaluate("new URLPattern({}).test()").AsBoolean().Should().BeTrue();
        engine.Evaluate("new URLPattern({ pathname: '/foo' }).test()").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void RejectsThePatternsTheStandardRejects()
    {
        var engine = WebEngine();

        // A constructor string that is relative and has no base URL.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern('/foo')"));

        // A dictionary input cannot also take a base URL argument.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern({ pathname: '/foo' }, 'https://example.com')"));

        // Two groups cannot share a name.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern({ pathname: '/:id/:id' })"));

        // A grouping has to be closed, and a regexp group cannot be empty or contain a capturing group.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern({ pathname: '/{foo' })"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern({ pathname: '/()' })"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern({ pathname: '/((a))' })"));

        // A regexp group whose source is not a valid regular expression.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URLPattern({ pathname: '/(a[)' })"));

        // ... and an exec whose input dictionary does not canonicalize is a non-match rather than a throw.
        engine.Evaluate("new URLPattern({}).exec({ port: 'invalid80' })").IsNull().Should().BeTrue();
    }

    [Fact]
    public void MembersBrandCheckTheirReceiver()
    {
        var engine = WebEngine();

        foreach (var expression in new[]
                 {
                     "Object.getOwnPropertyDescriptor(URLPattern.prototype, 'pathname').get.call({})",
                     "Object.getOwnPropertyDescriptor(URLPattern.prototype, 'hasRegExpGroups').get.call({})",
                     "URLPattern.prototype.test.call({}, {})",
                     "URLPattern.prototype.exec.call({}, {})",
                 })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate(expression))
                .Error.ToString().Should().Contain("TypeError");
        }

        // URLPattern.prototype itself is not a URLPattern.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URLPattern.prototype.pathname"));
    }

    [Fact]
    public void SubclassingUsesTheNewTargetsPrototype()
    {
        var engine = WebEngine();

        engine.Execute("class MyPattern extends URLPattern { }");
        engine.Execute("const p = new MyPattern({ pathname: '/foo' });");

        engine.Evaluate("p instanceof MyPattern").AsBoolean().Should().BeTrue();
        engine.Evaluate("p instanceof URLPattern").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.pathname").AsString().Should().Be("/foo");
    }

    [Fact]
    public void IsAbsentFromAShadowRealm()
    {
        var engine = WebEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof URLPattern')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof URLPattern").AsString().Should().Be("function");
    }

    [Fact]
    public void MatchingIsUnaffectedByAPatchedRegExpPrototypeExec()
    {
        var engine = WebEngine();

        engine.Execute("const p = new URLPattern({ pathname: '/:id' });");
        engine.Execute("RegExp.prototype.exec = function () { throw new Error('exec was called'); };");

        // The specification runs RegExpBuiltinExec, not RegExpExec, so nothing here reaches the replacement.
        engine.Evaluate("p.test({ pathname: '/42' })").AsBoolean().Should().BeTrue();
        engine.Evaluate("p.exec({ pathname: '/42' }).pathname.groups.id").AsString().Should().Be("42");
    }

    [Fact]
    public void ComponentRegularExpressionsAreBuiltByTheEnginesOwnRegExpMachinery()
    {
        // A structural pin on the claim the timeout test below rests on: each component's regular expression is a
        // real JsRegExp of this realm carrying the flags the standard names, so it is matched by the same code —
        // and under the same Options.Constraints.RegexTimeout — as any RegExp a script builds.
        var engine = WebEngine();

        var pattern = (JsUrlPattern) engine.Evaluate("new URLPattern({ pathname: '/:id' }, { ignoreCase: true })").AsObject();

        pattern.Pattern.Pathname.RegularExpression.Flags.Should().Be("vi");
        pattern.Pattern.Search.RegularExpression.Flags.Should().Be("vi");
        pattern.Pattern.Hash.RegularExpression.Flags.Should().Be("vi");

        // ... and only those three: the other five are compiled from the plain default or hostname options.
        pattern.Pattern.Protocol.RegularExpression.Flags.Should().Be("v");
        pattern.Pattern.Username.RegularExpression.Flags.Should().Be("v");
        pattern.Pattern.Password.RegularExpression.Flags.Should().Be("v");
        pattern.Pattern.Hostname.RegularExpression.Flags.Should().Be("v");
        pattern.Pattern.Port.RegularExpression.Flags.Should().Be("v");

        pattern.Pattern.Pathname.RegularExpression.Engine.Should().BeSameAs(engine);

        // The "v" flag takes every one of them onto the engine's own regexp interpreter, which is the path that
        // enforces the deadline; a .NET Regex would enforce Regex.MatchTimeout instead, and either way the source
        // of the timeout is the engine's configured constraint.
        pattern.Pattern.Pathname.RegularExpression.UsesDotNetEngine.Should().BeFalse();
    }

    [Fact]
    public void AHostileRegExpGroupEndsInTheEnginesRegexTimeout()
    {
        // The pattern below backtracks exponentially: the group has to partition a run of "a"s that can never be
        // followed by the "!" the anchored expression demands. The assertion is that the attempt is bounded, not
        // that it is fast — no machine finishes 2^40 backtracking steps, so nothing here can flake on timing.
        var engine = new Engine(options => options
            .UseWebApis(WebApiFeatures.Url)
            .Constraints.RegexTimeout = TimeSpan.FromMilliseconds(50));

        engine.Execute("const p = new URLPattern({ pathname: '/:x((?:a+)+)' });");
        engine.SetValue("hostileInput", "/" + new string('a', 40) + "!");

        var timeout = Assert.Throws<RegexMatchTimeoutException>(() => engine.Evaluate("p.test({ pathname: hostileInput })"));

        // The deadline is the engine's own regex constraint, not a constant of this feature's own.
        timeout.MatchTimeout.Should().Be(TimeSpan.FromMilliseconds(50));
    }
}
#endif
