#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// What <c>RegExpBuiltinExec</c> returns for the empty pattern <c>(?:)</c> - the shape every other pattern's result has
/// (https://tc39.es/ecma262/#sec-regexpbuiltinexec steps 22-36: the match, <c>index</c>, <c>input</c>, <c>groups</c>
/// as an own <c>undefined</c> when the pattern has no named groups) - and the legacy statics it updates. A shortcut
/// built the array without <c>groups</c> and without touching the statics. The expected strings are what Node prints
/// for the same script.
/// </summary>
public class RegExpEmptyPatternExecTests
{
    private const string Preamble =
        "var all = () => JSON.stringify([RegExp.$1, RegExp.$2, RegExp.$3, RegExp.$4, RegExp.$5, RegExp.$6, RegExp.$7, RegExp.$8, RegExp.$9, RegExp.lastMatch, RegExp.lastParen, RegExp.leftContext, RegExp.rightContext, RegExp.input]); ";

    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    // own keys
    [TestCase(Preamble + "Reflect.ownKeys(new RegExp('').exec('aaa')).join()", "0,length,index,input,groups")]
    // own keys of the literal
    [TestCase(Preamble + "Reflect.ownKeys(/(?:)/.exec('aaa')).join()", "0,length,index,input,groups")]
    // own keys under the flags that keep the shortcut
    [TestCase(Preamble + "['i', 'm', 's', 'u', 'v'].map(f => Reflect.ownKeys(new RegExp('', f).exec('aaa')).join()).join('|')", "0,length,index,input,groups|0,length,index,input,groups|0,length,index,input,groups|0,length,index,input,groups|0,length,index,input,groups")]
    // own keys under d
    [TestCase(Preamble + "Reflect.ownKeys(new RegExp('', 'd').exec('aaa')).join()", "0,length,index,input,groups,indices")]
    // own keys under g and y
    [TestCase(Preamble + "['g', 'y'].map(f => Reflect.ownKeys(new RegExp('', f).exec('aaa')).join()).join('|')", "0,length,index,input,groups|0,length,index,input,groups")]
    // groups is an own undefined
    [TestCase(Preamble + "var m = new RegExp('').exec('aaa'); JSON.stringify([m.hasOwnProperty('groups'), m.groups === undefined, 'groups' in m, m.index, m.input, m.length, m[0]])", "[true,true,true,0,\"aaa\",1,\"\"]")]
    // the object spread
    [TestCase(Preamble + "JSON.stringify({ ...new RegExp('').exec('aaa') })", "{\"0\":\"\",\"index\":0,\"input\":\"aaa\"}")]
    // entries
    [TestCase(Preamble + "JSON.stringify(Object.entries(new RegExp('').exec('aaa')))", "[[\"0\",\"\"],[\"index\",0],[\"input\",\"aaa\"],[\"groups\",null]]")]
    // for-in
    [TestCase(Preamble + "var k = []; for (var p in new RegExp('').exec('aaa')) k.push(p); k.join()", "0,index,input,groups")]
    // attributes
    [TestCase(Preamble + "['0', 'index', 'input', 'groups'].map(k => { var d = Object.getOwnPropertyDescriptor(new RegExp('').exec('aaa'), k); return k + ':' + d.writable + d.enumerable + d.configurable; }).join()", "0:truetruetrue,index:truetruetrue,input:truetruetrue,groups:truetruetrue")]
    // length
    [TestCase(Preamble + "var d = Object.getOwnPropertyDescriptor(new RegExp('').exec('aaa'), 'length'); [d.value, d.writable, d.enumerable, d.configurable].join()", "1,true,false,false")]
    // mutable
    [TestCase(Preamble + "var m = new RegExp('').exec('aaa'); m.index = 4; m.groups = 1; delete m.input; m.extra = 1; JSON.stringify([m.index, m.groups, m.input, Object.keys(m)])", "[4,1,null,[\"0\",\"index\",\"groups\",\"extra\"]]")]
    // lastIndex is ignored
    [TestCase(Preamble + "var re = /(?:)/; re.lastIndex = 2; var m = re.exec('aaa'); JSON.stringify([m.index, re.lastIndex])", "[0,2]")]
    // match
    [TestCase(Preamble + "var m = 'aaa'.match(/(?:)/); Reflect.ownKeys(m).join()", "0,length,index,input,groups")]
    // match with no argument
    [TestCase(Preamble + "var m = 'aaa'.match(); JSON.stringify([Reflect.ownKeys(m).join(), m.index, m.input, m.groups === undefined])", "[\"0,length,index,input,groups\",0,\"aaa\",true]")]
    // search with no argument
    [TestCase(Preamble + "'aaa'.search()", "0")]
    // the empty subject
    [TestCase(Preamble + "var m = new RegExp('').exec(''); JSON.stringify([Reflect.ownKeys(m).join(), m.index, m.input, m.length])", "[\"0,length,index,input,groups\",0,\"\",1]")]
    // statics
    [TestCase(Preamble + "/(z)(y)/.exec('xzy!'); new RegExp('').exec('aaa'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"aaa\",\"aaa\"]")]
    // statics on an empty subject
    [TestCase(Preamble + "/(z)(y)/.exec('xzy!'); new RegExp('').exec(''); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\"]")]
    // statics of the literal
    [TestCase(Preamble + "/(z)/.exec('xz'); /(?:)/.exec('abc'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"abc\",\"abc\"]")]
    // statics under i
    [TestCase(Preamble + "/(z)/.exec('xz'); /(?:)/i.exec('abc'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"abc\",\"abc\"]")]
    // statics through match
    [TestCase(Preamble + "/(z)/.exec('xz'); 'aaa'.match(/(?:)/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"aaa\",\"aaa\"]")]
    // statics through match with no argument
    [TestCase(Preamble + "/(z)/.exec('xz'); 'aaa'.match(); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"aaa\",\"aaa\"]")]
    // statics through search
    [TestCase(Preamble + "/(z)/.exec('xz'); 'aaa'.search(/(?:)/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"aaa\",\"aaa\"]")]
    // statics after a read
    [TestCase(Preamble + "/(z)/.exec('xz'); var r1 = RegExp.$1; new RegExp('').exec('aaa'); JSON.stringify([r1, RegExp.$1, RegExp.lastMatch, RegExp.input])", "[\"z\",\"\",\"\",\"aaa\"]")]
    // input assignment first
    [TestCase(Preamble + "RegExp.input = 'seed'; new RegExp('').exec('aaa'); RegExp.input", "aaa")]
    // a result written before the statics
    [TestCase(Preamble + "var re = new RegExp(''); var a = re.exec('abc'); var b = re.exec('xyz'); JSON.stringify([a.input, b.input, RegExp.input, RegExp.rightContext])", "[\"abc\",\"xyz\",\"xyz\",\"xyz\"]")]
    public void TheEmptyPatternResultIsTheSpecifiedResult(string script, string expected)
    {
        Run(script).Should().Be(expected);
    }
}
