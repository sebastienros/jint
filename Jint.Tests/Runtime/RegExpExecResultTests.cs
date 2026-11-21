#nullable enable

using System.Text.RegularExpressions;

namespace Jint.Tests.Runtime;

/// <summary>
/// The array <c>RegExpBuiltinExec</c> returns: elements <c>0</c>..<c>n</c>, then <c>index</c>, <c>input</c>,
/// <c>groups</c> and, under <c>/d</c>, <c>indices</c> - each a writable, enumerable, configurable data property.
/// https://tc39.es/ecma262/#sec-regexpbuiltinexec steps 22-36. The expected strings are what Node prints for the same
/// script; the result is built by installing those properties directly, so these pin that doing so left the observable
/// array - its key order, its attributes, how it reacts to being redefined - exactly what
/// <c>CreateDataPropertyOrThrow</c> would have produced.
/// </summary>
public class RegExpExecResultTests
{
    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    // keys of a plain exec result
    [TestCase("Object.keys(/(a)(b)?/.exec('xab')).join()", "0,1,2,index,input,groups")]
    // ownKeys of a plain exec result
    [TestCase("Reflect.ownKeys(/(a)(b)?/.exec('xab')).join()", "0,1,2,length,index,input,groups")]
    // ownKeys with named groups and indices
    [TestCase("Reflect.ownKeys(/(?<x>a)(?<y>b)?/d.exec('xab')).join()", "0,1,2,length,index,input,groups,indices")]
    // keys of the indices array
    [TestCase("Object.keys(/(?<x>a)/d.exec('xa').indices).join() + '|' + Reflect.ownKeys(/(?<x>a)/d.exec('xa').indices).join()", "0,1,groups|0,1,length,groups")]
    // no captures
    [TestCase("Reflect.ownKeys(/b/.exec('abc')).join()", "0,length,index,input,groups")]
    // sticky
    [TestCase("var r = /(b)/y; r.lastIndex = 1; Reflect.ownKeys(r.exec('abc')).join()", "0,1,length,index,input,groups")]
    // global
    [TestCase("var r = /(b)/g; Reflect.ownKeys(r.exec('abc')).join()", "0,1,length,index,input,groups")]
    // spread into an object
    [TestCase("JSON.stringify(Object.assign({}, /(?<x>a)(b)/d.exec('zab')))", "{\"0\":\"ab\",\"1\":\"a\",\"2\":\"b\",\"index\":1,\"input\":\"zab\",\"groups\":{\"x\":\"a\"},\"indices\":[[1,3],[1,2],[2,3]]}")]
    // object spread
    [TestCase("JSON.stringify({ ...(/(?<x>a)(b)?/.exec('za')) })", "{\"0\":\"a\",\"1\":\"a\",\"index\":1,\"input\":\"za\",\"groups\":{\"x\":\"a\"}}")]
    // for-in
    [TestCase("var k = []; for (var p in /(a)/.exec('xa')) k.push(p); k.join()", "0,1,index,input,groups")]
    // entries
    [TestCase("JSON.stringify(Object.entries(/(a)(?<n>b)/.exec('xab')))", "[[\"0\",\"ab\"],[\"1\",\"a\"],[\"2\",\"b\"],[\"index\",1],[\"input\",\"xab\"],[\"groups\",{\"n\":\"b\"}]]")]
    // attributes
    [TestCase("['index', 'input', 'groups', 'indices'].map(k => { var d = Object.getOwnPropertyDescriptor(/(a)/d.exec('xa'), k); return k + ':' + d.writable + d.enumerable + d.configurable + ('value' in d); }).join()", "index:truetruetruetrue,input:truetruetruetrue,groups:truetruetruetrue,indices:truetruetruetrue")]
    // element attributes
    [TestCase("var d = Object.getOwnPropertyDescriptor(/(a)/.exec('xa'), '1'); [d.writable, d.enumerable, d.configurable].join()", "true,true,true")]
    // length attributes
    [TestCase("var d = Object.getOwnPropertyDescriptor(/(a)/.exec('xa'), 'length'); [d.value, d.writable, d.enumerable, d.configurable].join()", "2,true,false,false")]
    // named groups
    [TestCase("var m = /(a)(?<n>b)?(c)?/.exec('xac'); JSON.stringify([m.index, m.input, Object.getPrototypeOf(m.groups), Object.keys(m.groups), m.groups.n === undefined, 'n' in m.groups, m.length, m[1], m[2] === undefined, m[3]])", "[1,\"xac\",null,[\"n\"],true,true,4,\"a\",true,\"c\"]")]
    // no named groups
    [TestCase("var m = /b/.exec('abc'); JSON.stringify([m.index, m.input, 'groups' in m, m.groups === undefined, m.hasOwnProperty('groups'), m.hasOwnProperty('indices')])", "[1,\"abc\",true,true,true,false]")]
    // indices
    [TestCase("var m = /(a)(?<n>b)?(c)/d.exec('xac'); JSON.stringify([m.indices, Object.keys(m.indices.groups), m.indices.groups.n === undefined, Object.getPrototypeOf(m.indices.groups), m.indices.length])", "[[[1,3],[1,2],null,[2,3]],[\"n\"],true,null,4]")]
    // indices without named groups
    [TestCase("var m = /(a)(c)/d.exec('xac'); JSON.stringify([m.indices, 'groups' in m.indices, m.indices.groups === undefined, Reflect.ownKeys(m.indices).join()])", "[[[1,3],[1,2],[2,3]],true,true,\"0,1,2,length,groups\"]")]
    // duplicate named groups
    [TestCase("var m = /(?:(?<n>a)|(?<n>b))/.exec('xb'); JSON.stringify([Object.keys(m.groups), m.groups.n])", "[[\"n\"],\"b\"]")]
    // duplicate named groups with indices
    [TestCase("var m = /(?:(?<n>a)|(?<n>b))/d.exec('xb'); JSON.stringify([Object.keys(m.groups), m.groups.n, m.indices.groups.n, Reflect.ownKeys(m).join()])", "[[\"n\"],\"b\",[1,2],\"0,1,2,length,index,input,groups,indices\"]")]
    // index of a later match
    [TestCase("var m = /\\d+/.exec('ab 123'); JSON.stringify([m.index, m.input, m[0]])", "[3,\"ab 123\",\"123\"]")]
    // writable and configurable
    [TestCase("var m = /(a)/.exec('xa'); m.index = 7; m.input = 'q'; m.groups = 1; var s = JSON.stringify([m.index, m.input, m.groups]); delete m.index; m.extra = 1; s + JSON.stringify([m.index, Object.keys(m)])", "[7,\"q\",1][null,[\"0\",\"1\",\"input\",\"groups\",\"extra\"]]")]
    // extensible
    [TestCase("var m = /(a)/.exec('xa'); [Object.isExtensible(m), Object.isFrozen(m), Object.isSealed(m), Array.isArray(m)].join()", "true,false,false,true")]
    // frozen result
    [TestCase("var m = Object.freeze(/(a)/.exec('xa')); m.index = 9; [m.index, Object.isFrozen(m)].join()", "1,true")]
    // defineProperty over a result property
    [TestCase("var m = /(a)/.exec('xa'); Object.defineProperty(m, 'index', { get() { return 42; }, configurable: true, enumerable: true }); [m.index, Object.keys(m).join()].join('|')", "42|0,1,index,input,groups")]
    // independent results
    [TestCase("var re = /(\\d)/; var a = re.exec('a1'); var b = re.exec('b2'); JSON.stringify([a.index, a.input, a[1], b.index, b.input, b[1], a === b])", "[1,\"a1\",\"1\",1,\"b2\",\"2\",false]")]
    // match
    [TestCase("Object.keys('xbc'.match(/(b)/)).join()", "0,1,index,input,groups")]
    // match global
    [TestCase("Object.keys('abab'.match(/(a)/g)).join()", "0,1")]
    // matchAll
    [TestCase("var r = [...'a1b2'.matchAll(/(?<d>\\d)/g)]; JSON.stringify(r.map(m => [m.index, m.input, Reflect.ownKeys(m).join(), m.groups.d]))", "[[1,\"a1b2\",\"0,1,length,index,input,groups\",\"1\"],[3,\"a1b2\",\"0,1,length,index,input,groups\",\"2\"]]")]
    // match indices
    [TestCase("var m = 'xab'.match(/(?<q>a)(b)/d); JSON.stringify([m.indices[2], m.indices.groups.q, m.index])", "[[2,3],[1,2],1]")]
    // replace callback groups
    [TestCase("'xab'.replace(/(?<q>a)(b)/, (...a) => JSON.stringify([a[a.length - 1], a.length]))", "x[{\"q\":\"a\"},6]")]
    // repeated reads through a warmed site
    [TestCase("var m = /(a)(b)/.exec('xab'); var t = 0; for (var i = 0; i < 50; i++) { t += m.index; t += m.input.length; if (m.groups !== undefined) t += 1000; } t", "200")]
    // reads across results of one site
    [TestCase("var re = /(\\d+)/; var t = 0; for (var i = 0; i < 20; i++) { var m = re.exec('x'.repeat(i) + '5'); t += m.index + m.input.length; } t", "400")]
    // computed key reads
    [TestCase("var m = /(a)/d.exec('xa'); ['index', 'input', 'groups', 'indices'].map(k => typeof m[k]).join()", "number,string,undefined,object")]
    // in and hasOwnProperty
    [TestCase("var m = /(a)/.exec('xa'); ['index', 'input', 'groups', 'indices'].map(k => (k in m) + ':' + m.hasOwnProperty(k) + ':' + Object.prototype.propertyIsEnumerable.call(m, k)).join()", "true:true:true,true:true:true,true:true:true,false:false:false")]
    // array methods see only elements
    [TestCase("var m = /(a)(b)?/.exec('xa'); JSON.stringify([m.map(x => x === undefined ? 'u' : x), m.slice(), [...m], Array.from(m), JSON.stringify(m)])", "[[\"a\",\"a\",\"u\"],[\"a\",\"a\",null],[\"a\",\"a\",null],[\"a\",\"a\",null],\"[\\\"a\\\",\\\"a\\\",null]\"]")]
    // concat and length
    [TestCase("var m = /(a)(b)(c)/.exec('abc'); [m.length, m.concat([1]).length, Array.isArray(m)].join()", "4,5,true")]
    // delete then re-add
    [TestCase("var m = /(a)/.exec('xa'); delete m.index; m.index = 3; Reflect.ownKeys(m).join()", "0,1,length,input,groups,index")]
    // lastIndex untouched for non-global
    [TestCase("var r = /(a)/; r.exec('xa'); r.lastIndex", "0")]
    public void TheResultMatchesNode(string script, string expected)
    {
        Run(script).Should().Be(expected);
    }

    // The .NET lane builds the same array for a Regex the host handed over.
    [Test]
    public void AHostSuppliedRegexBuildsTheSameShape()
    {
        var engine = new Engine();
        engine.SetValue("host", new Regex("([a-z])(?<n>b)?"));

        engine.Evaluate("Reflect.ownKeys(host.exec('xa')).join()").ToString().Should().Be("0,1,2,length,index,input,groups");
        engine.Evaluate("var m = host.exec('xb'); JSON.stringify([m.index, m.input, m.groups.n, Object.keys(m.groups)])").ToString()
            .Should().Be("""[0,"xb","b",["n"]]""");
        engine.Evaluate("['index', 'input', 'groups'].map(k => { var d = Object.getOwnPropertyDescriptor(host.exec('xb'), k); return d.writable && d.enumerable && d.configurable; }).join()").ToString()
            .Should().Be("true,true,true");
    }
}
