#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Runtime;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// https://urlpattern.spec.whatwg.org/#component — one compiled URL component of a pattern: its normalized
/// pattern string, the regular expression that matches it, and the names of that expression's capture groups.
/// </summary>
/// <remarks>
/// <para>
/// The regular expression is a real JavaScript <c>RegExp</c> built through the engine's own
/// <c>RegExpCreate</c> with the "<c>v</c>" flag the specification names, and matching goes through
/// <c>RegExpBuiltinExec</c>. That is not incidental: a "<c>(regexp)</c>" group holds arbitrary JavaScript
/// regular expression source, which only the engine's own regexp machinery can be trusted to read. It also means
/// a component inherits that machinery's guards unchanged — in particular <c>Options.Constraints.RegexTimeout</c>,
/// so a pattern written to backtrack catastrophically ends in the same <c>RegexMatchTimeoutException</c> a
/// hostile <c>RegExp</c> literal would, rather than hanging the host.
/// </para>
/// <para>
/// The specification notes that using regular expressions for matching is not mandated and that an implementation
/// may match a part list directly. This one does not: correctness for author-supplied regexp groups is worth more
/// here than avoiding the <c>RegExp</c> object, and the "<c>v</c>" flag's semantics — code-point-wise
/// <c>[^/]</c>, a "<c>.</c>" that excludes every line terminator — are exactly what the engine already implements
/// and a hand-rolled matcher would have to reproduce.
/// </para>
/// </remarks>
internal sealed class UrlPatternComponent
{
    /// <summary>https://url.spec.whatwg.org/#special-scheme</summary>
    private static readonly string[] _specialSchemes = ["ftp", "file", "http", "https", "ws", "wss"];

    private UrlPatternComponent(string patternString, JsRegExp regularExpression, string[] groupNameList, bool hasRegexpGroups)
    {
        PatternString = patternString;
        RegularExpression = regularExpression;
        GroupNameList = groupNameList;
        HasRegexpGroups = hasRegexpGroups;
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#component-pattern-string</summary>
    internal string PatternString { get; }

    /// <summary>https://urlpattern.spec.whatwg.org/#component-regular-expression</summary>
    internal JsRegExp RegularExpression { get; }

    /// <summary>https://urlpattern.spec.whatwg.org/#component-group-name-list</summary>
    internal string[] GroupNameList { get; }

    /// <summary>https://urlpattern.spec.whatwg.org/#component-has-regexp-groups</summary>
    internal bool HasRegexpGroups { get; }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#compile-a-component
    /// </summary>
    internal static UrlPatternComponent Compile(
        Engine engine,
        string input,
        UrlPatternEncodingCallback encodingCallback,
        UrlPatternCompileOptions options)
    {
        var realm = engine.Realm;
        var partList = UrlPatternStringParser.Parse(realm, input, options, encodingCallback);
        var (source, nameList) = UrlPatternGenerator.GenerateRegularExpressionAndNameList(partList, options);

        // An author-supplied "(regexp)" group is compiled here rather than lazily, so that an invalid one is a
        // TypeError from the constructor instead of a surprise on a later match.
        JsRegExp regularExpression;
        try
        {
            regularExpression = realm.Intrinsics.RegExp.RegExpCreate(JsString.Create(source), options.IgnoreCase ? "vi" : "v");
        }
        catch (JavaScriptException)
        {
            Throw.TypeError(realm, $"Invalid regular expression in URLPattern component: {input}");
            return null!;
        }

        var hasRegexpGroups = false;
        foreach (var part in partList)
        {
            if (part.Type == UrlPatternPartType.Regexp)
            {
                hasRegexpGroups = true;
                break;
            }
        }

        return new UrlPatternComponent(
            UrlPatternGenerator.GeneratePatternString(partList, options),
            regularExpression,
            nameList,
            hasRegexpGroups);
    }

    /// <summary>
    /// <c>RegExpBuiltinExec</c> against this component's regular expression, which is what
    /// https://urlpattern.spec.whatwg.org/#url-pattern-match runs for every component. The built-in is called
    /// directly rather than through <c>RegExpExec</c>, so a script that replaced
    /// <c>RegExp.prototype.exec</c> cannot observe or alter a <c>URLPattern</c> match.
    /// </summary>
    internal JsValue Exec(string input) => RegExpPrototype.RegExpBuiltinExec(RegularExpression, input);

    /// <summary>https://urlpattern.spec.whatwg.org/#protocol-component-matches-a-special-scheme</summary>
    internal bool MatchesSpecialScheme()
    {
        foreach (var scheme in _specialSchemes)
        {
            if (!Exec(scheme).IsNull())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#create-a-component-match-result — the
    /// <c>URLPatternComponentResult</c> for one component: the string that was matched, and a record from group
    /// name to the value that group captured, <see langword="undefined"/> where it did not participate.
    /// </summary>
    internal ObjectInstance CreateComponentMatchResult(Engine engine, string input, JsValue execResult)
    {
        var groups = ObjectInstance.OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        var match = (ObjectInstance) execResult;

        for (var index = 1; index <= GroupNameList.Length; index++)
        {
            groups.CreateDataPropertyOrThrow(JsString.Create(GroupNameList[index - 1]), match.Get(JsString.Create(index)));
        }

        return JsObject.Create(engine, UrlPatternResultLayouts.ComponentResult, [JsString.Create(input), groups]);
    }
}

/// <summary>
/// The two WebIDL dictionaries a match produces. Declaring them as layouts means every result object in an engine
/// shares one hidden class, so a caller reading <c>.pathname.groups</c> off many results keeps a monomorphic
/// inline cache.
/// </summary>
internal static class UrlPatternResultLayouts
{
    /// <summary>https://urlpattern.spec.whatwg.org/#dictdef-urlpatterncomponentresult</summary>
    internal static readonly JsObjectLayout ComponentResult = JsObjectLayout.CreateBuilder()
        .Add("input")
        .Add("groups")
        .Build();

    /// <summary>https://urlpattern.spec.whatwg.org/#dictdef-urlpatternresult</summary>
    internal static readonly JsObjectLayout Result = JsObjectLayout.CreateBuilder()
        .Add("inputs")
        .Add("protocol")
        .Add("username")
        .Add("password")
        .Add("hostname")
        .Add("port")
        .Add("pathname")
        .Add("search")
        .Add("hash")
        .Build();
}
#endif
