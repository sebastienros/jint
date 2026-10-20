#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A generator resumed with <c>return(value)</c> or <c>throw(value)</c> while it is suspended on a <c>yield</c> inside
/// an expression. <see href="https://tc39.es/ecma262/#sec-generatorresumeabrupt">GeneratorResumeAbrupt</see> resumes it
/// with that abrupt completion, the <see href="https://tc39.es/ecma262/#sec-yield">yield</see> evaluates to it, and
/// every <c>?</c> between the yield and its statement propagates it, so nothing that would follow the yield runs: no
/// operator, no call, no <c>PutValue</c>, no branch. The <c>finally</c> then sees the bindings as they were.
/// <para>
/// A return completion used to come back from the yield as an ordinary value, so the expression carried on with it:
/// <c>n += yield 0</c> stored <c>n + value</c> before the <c>finally</c> ran, an argument list called its function,
/// an <c>if</c> took a branch, a <c>yield</c> around the yield yielded again, and a brace-less <c>while</c> body never
/// ended (sebastienros/jint#4304). A throw completion was already raised at the yield; it is pinned beside each row so
/// the two stay alike. Every expected string is Node 24's answer to the same script.
/// </para>
/// </summary>
public class SuspendedAbruptResumptionTests
{
    // RV logs every way an expression could consume it, so a return value that leaks into the expression shows in the
    // log; side() and key() log the operands the expression must not reach.
    private const string Prelude = """
        var log = [];
        var RV = {
          valueOf() { log.push('RV.valueOf'); return 3; },
          toString() { log.push('RV.toString'); return 'RV'; },
          get prop() { log.push('RV.prop'); return 1; },
          [Symbol.iterator]() { log.push('RV.iterator'); return [][Symbol.iterator](); },
        };
        var ERR = { name: 'ERR' };
        var o;
        function side(tag) { log.push('side ' + tag); return tag; }
        function key() { log.push('key'); return 'n'; }
        function closable() {
          return { [Symbol.iterator]() { return { next() { return { value: 1, done: false }; }, return() { log.push('iterator closed'); return {}; } }; } };
        }
        function show(r) { return '{' + (r.value === RV ? 'RV' : String(r.value)) + ',' + r.done + '}'; }
        function shown(e) { return 'threw ' + (e === ERR ? 'ERR' : String(e)); }

        """;

    private static string RunGenerator(string body, string resumption)
    {
        var script = Prelude
                     + "function* inner() { try { yield 'i1'; yield 'i2'; return 'ir'; } finally { log.push('inner fin'); } }\n"
                     + "var it = (function* () { " + body + " })();\n"
                     + "var res = [show(it.next())];\n"
                     + "try { res.push(show(it." + resumption + ")); res.push(show(it.next())); } catch (e) { res.push(shown(e)); }\n"
                     + "res.join(' ') + ' | ' + log.join(', ');";
        return new Engine().Evaluate(script).AsString();
    }

    private static string RunAsyncGenerator(string body, string resumption)
    {
        var script = Prelude
                     + "async function* inner() { try { yield 'i1'; yield 'i2'; return 'ir'; } finally { log.push('inner fin'); } }\n"
                     + "var it = (async function* () { " + body + " })();\n"
                     + "(async () => {\n"
                     + "  var res = [show(await it.next())];\n"
                     + "  try { res.push(show(await it." + resumption + ")); res.push(show(await it.next())); } catch (e) { res.push(shown(e)); }\n"
                     + "  return res.join(' ') + ' | ' + log.join(', ');\n"
                     + "})();";
        return new Engine().Evaluate(script).UnwrapIfPromise().AsString();
    }

    /// <summary>
    /// A name, a generator body that suspends once, and what the generator answers to <c>next()</c>, then
    /// <c>return(RV)</c> (or <c>throw(ERR)</c>), then <c>next()</c>, followed by what it logged.
    /// </summary>
    private readonly record struct Row(string Name, string Body, string AfterReturn, string AfterThrow);

    private static readonly Row[] GeneratorRows =
    [
        new(
            "CompoundAssignmentToVar",
            "var n = 1; try { n += yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=1",
            "{0,false} threw ERR | n=1"),
        new(
            "CompoundAssignmentToLet",
            "let n = 1; try { n -= yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=1",
            "{0,false} threw ERR | n=1"),
        new(
            "CompoundAssignmentToProperty",
            "o = { n: 1 }; try { o.n += yield 0; } finally { log.push('o.n=' + o.n); }",
            "{0,false} {RV,true} {undefined,true} | o.n=1",
            "{0,false} threw ERR | o.n=1"),
        new(
            "CompoundAssignmentToComputedProperty",
            "o = { n: 1 }; try { o[key()] *= yield 0; } finally { log.push('o.n=' + o.n); }",
            "{0,false} {RV,true} {undefined,true} | key, o.n=1",
            "{0,false} threw ERR | key, o.n=1"),
        new(
            "CompoundAssignmentToSetter",
            "o = { get n() { return 1; }, set n(v) { log.push('set ' + v); } }; try { o.n -= yield 0; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "LogicalOrAssignment",
            "var n = 0; try { n ||= yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=0",
            "{0,false} threw ERR | n=0"),
        new(
            "LogicalAndAssignment",
            "var n = 1; try { n &&= yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=1",
            "{0,false} threw ERR | n=1"),
        new(
            "NullishAssignment",
            "var n = null; try { n ??= yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=null",
            "{0,false} threw ERR | n=null"),
        new(
            "NullishAssignmentToProperty",
            "o = { n: null }; try { o.n ??= yield 0; } finally { log.push('o.n=' + o.n); }",
            "{0,false} {RV,true} {undefined,true} | o.n=null",
            "{0,false} threw ERR | o.n=null"),
        new(
            "AssignmentToVar",
            "var n = 1; try { n = yield 0; } finally { log.push('n=' + (n === RV ? 'RV' : n)); }",
            "{0,false} {RV,true} {undefined,true} | n=1",
            "{0,false} threw ERR | n=1"),
        new(
            "AssignmentToProperty",
            "o = { n: 1 }; try { o.n = yield 0; } finally { log.push('o.n=' + (o.n === RV ? 'RV' : o.n)); }",
            "{0,false} {RV,true} {undefined,true} | o.n=1",
            "{0,false} threw ERR | o.n=1"),
        new(
            "AssignmentToSetter",
            "o = { set n(v) { log.push('set'); } }; try { o.n = yield 0; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "AssignmentToComputedProperty",
            "o = {}; try { o[key()] = yield 0; } finally { log.push('keys=' + Object.keys(o)); }",
            "{0,false} {RV,true} {undefined,true} | key, keys=",
            "{0,false} threw ERR | key, keys="),
        new(
            "VariableDeclaration",
            "try { var v = yield 0; } finally { log.push('v=' + (v === RV ? 'RV' : v)); }",
            "{0,false} {RV,true} {undefined,true} | v=undefined",
            "{0,false} threw ERR | v=undefined"),
        new(
            "ArrayDestructuringOfTheYield",
            "var a = 1; try { [a] = yield 0; } finally { log.push('a=' + a); }",
            "{0,false} {RV,true} {undefined,true} | a=1",
            "{0,false} threw ERR | a=1"),
        new(
            "ObjectDestructuringOfTheYield",
            "var a = 1; try { ({ prop: a } = yield 0); } finally { log.push('a=' + a); }",
            "{0,false} {RV,true} {undefined,true} | a=1",
            "{0,false} threw ERR | a=1"),
        new(
            "ArrayDestructuringDefault",
            "var a = 1, b = 1; try { [a = yield 0, b = side('b')] = []; } finally { log.push('a=' + a + ' b=' + b); }",
            "{0,false} {RV,true} {undefined,true} | a=1 b=1",
            "{0,false} threw ERR | a=1 b=1"),
        new(
            "ObjectDestructuringDefault",
            "var a = 1, b = 1; try { ({ x: a = yield 0, y: b = side('b') } = {}); } finally { log.push('a=' + a + ' b=' + b); }",
            "{0,false} {RV,true} {undefined,true} | a=1 b=1",
            "{0,false} threw ERR | a=1 b=1"),
        new(
            "BindingPatternDefault",
            "try { var [p = yield 0, q = side('q')] = []; } finally { log.push('p=' + p + ' q=' + q); }",
            "{0,false} {RV,true} {undefined,true} | p=undefined q=undefined",
            "{0,false} threw ERR | p=undefined q=undefined"),
        new(
            "FirstCallArgument",
            "function f() { log.push('f called'); } try { f(yield 0, side('arg2')); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "LastCallArgument",
            "function f() { log.push('f called'); } try { f(side('arg1'), yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | side arg1, fin",
            "{0,false} threw ERR | side arg1, fin"),
        new(
            "MethodCallArgument",
            "o = { m() { log.push('m called'); } }; try { o.m(yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "SpreadCallArgument",
            "function f() { log.push('f called'); } try { f(...(yield 0)); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "OptionalCallArgument",
            "o = { m() { log.push('m called'); } }; try { o?.m(yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "CallChainArgument",
            "o = { m() { log.push('m'); return o; } }; try { o.m(yield 0).m(); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ConstructorArgument",
            "function F() { log.push('F called'); } try { new F(yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "TaggedTemplateSubstitution",
            "function tag() { log.push('tag called'); } try { tag`${yield 0}`; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "BinaryRightOperand",
            "var lhs = { valueOf() { log.push('lhs.valueOf'); return 1; } }; try { lhs + (yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "BinaryLeftOperand",
            "try { (yield 0) + side('r'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "RelationalOperand",
            "try { (yield 0) < side('r'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "InOperand",
            "var p = new Proxy({}, { has() { log.push('has trap'); return true; } }); try { (yield 0) in p; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "UnaryOperand",
            "try { -(yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "MemberObject",
            "try { (yield 0).prop; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ComputedMemberObject",
            "try { (yield 0)[side('key')]; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "UpdateOfComputedMember",
            "o = { RV: 1 }; try { o[yield 0]++; } finally { log.push('o.RV=' + o.RV); }",
            "{0,false} {RV,true} {undefined,true} | o.RV=1",
            "{0,false} threw ERR | o.RV=1"),
        new(
            "TemplateSubstitution",
            "try { `${yield 0}${side('t2')}`; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "TemplateSubstitutionAfterAnother",
            "var lhs = { toString() { log.push('lhs.toString'); return 'L'; } }; try { `${lhs}${yield 0}`; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | lhs.toString, fin",
            "{0,false} threw ERR | lhs.toString, fin"),
        new(
            "ObjectLiteralValue",
            "try { ({ a: yield 0, b: side('b') }); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ObjectLiteralSpread",
            "try { ({ ...(yield 0) }); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ArrayLiteralElement",
            "try { [yield 0, side('b')]; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ArrayLiteralSpread",
            "try { [...(yield 0)]; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ConditionalTest",
            "try { (yield 0) ? side('t') : side('f'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "LogicalOrLeftOperand",
            "try { !(yield 0) || side('r'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "LogicalAndLeftOperand",
            "try { (yield 0) && side('r'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "NullishLeftOperand",
            "try { void (yield 0) ?? side('r'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "SequenceOperand",
            "try { (yield 0, side('r')); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "YieldOperand",
            "try { yield (yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ClassHeritage",
            "try { (class extends (yield 0) {}); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ClassComputedMethodKey",
            "try { (class { [yield 0]() {} [side('k2')]() {} }); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ClassComputedFieldKey",
            "try { (class { [yield 0] = 1; }); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "DelegationInCompoundAssignment",
            "var n = 1; try { n += yield* inner(); } finally { log.push('n=' + n); }",
            "{i1,false} {RV,true} {undefined,true} | inner fin, n=1",
            "{i1,false} threw ERR | inner fin, n=1"),
        new(
            "DelegationAsCallArgument",
            "function f() { log.push('f called'); } try { f(yield* inner()); } finally { log.push('fin'); }",
            "{i1,false} {RV,true} {undefined,true} | inner fin, fin",
            "{i1,false} threw ERR | inner fin, fin"),
        new(
            "DelegationAsStatement",
            "try { yield* inner(); side('after'); } finally { log.push('fin'); }",
            "{i1,false} {RV,true} {undefined,true} | inner fin, fin",
            "{i1,false} threw ERR | inner fin, fin"),
        new(
            "IfTest",
            "try { if (yield 0) side('then'); else side('else'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "IfTestWithBlocks",
            "try { if (yield 0) { side('then'); } else { side('else'); } } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "WhileTest",
            "try { while (yield 0) { side('body'); break; } } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "DoWhileTest",
            "try { do { side('body'); } while (yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | side body, fin",
            "{0,false} threw ERR | side body, fin"),
        new(
            "ForTest",
            "try { for (; yield 0;) { side('body'); break; } } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ForUpdate",
            "try { for (var i = 0; i < 2; i += yield 0) { side('body ' + i); } } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | side body 0, fin",
            "{0,false} threw ERR | side body 0, fin"),
        new(
            "SwitchDiscriminant",
            "try { switch (yield 0) { default: side('default'); } } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "SwitchCaseTest",
            "try { switch (1) { case (yield 0): side('case'); break; default: side('default'); } } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ThrowArgument",
            "try { throw yield 0; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ReturnArgument",
            "try { return yield 0; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ForOfHead",
            "try { for (var x of yield 0) side('body'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "ForInHead",
            "try { for (var x in yield 0) side('body'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "WithObject",
            "try { with (yield 0) side('body'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "CatchClauseDoesNotSeeTheReturn",
            "var n = 1; try { n += yield 0; } catch (e) { log.push('caught'); } log.push('after');",
            "{0,false} {RV,true} {undefined,true} | ",
            "{0,false} {undefined,true} {undefined,true} | caught, after"),
        new(
            "WhileBodyWithoutBraces",
            "var i = 0; try { while (true) yield i++; } finally { log.push('fin i=' + i); }",
            "{0,false} {RV,true} {undefined,true} | fin i=1",
            "{0,false} threw ERR | fin i=1"),
        new(
            "DoWhileBodyWithoutBraces",
            "var i = 0; try { do yield i++; while (true); } finally { log.push('fin i=' + i); }",
            "{0,false} {RV,true} {undefined,true} | fin i=1",
            "{0,false} threw ERR | fin i=1"),
        new(
            "ForBodyWithoutBraces",
            "try { for (var i = 0; ; i++) yield i; } finally { log.push('fin i=' + i); }",
            "{0,false} {RV,true} {undefined,true} | fin i=0",
            "{0,false} threw ERR | fin i=0"),
        new(
            "ForOfBodyWithoutBraces",
            "try { for (var x of [0, 1, 2]) yield x; } finally { log.push('fin x=' + x); }",
            "{0,false} {RV,true} {undefined,true} | fin x=0",
            "{0,false} threw ERR | fin x=0"),
        new(
            "ForOfBodyWithoutBracesClosesTheIterator",
            "var n = 1; try { for (var x of closable()) n += yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | iterator closed, n=1",
            "{0,false} threw ERR | iterator closed, n=1"),
        new(
            "ForOfBodyWithBracesClosesTheIterator",
            "var n = 1; try { for (var x of closable()) { n += yield 0; } } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | iterator closed, n=1",
            "{0,false} threw ERR | iterator closed, n=1"),
        new(
            "LabelledYield",
            "try { l: yield 0; side('after'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "SingleStatementBlockInALoop",
            "var i = 0; try { while (true) { yield i++; } } finally { log.push('fin i=' + i); }",
            "{0,false} {RV,true} {undefined,true} | fin i=1",
            "{0,false} threw ERR | fin i=1"),
    ];

    // The same for an async generator, whose return() goes through the request queue and awaits the value.
    private static readonly Row[] AsyncGeneratorRows =
    [
        new(
            "CompoundAssignmentToVar",
            "var n = 1; try { n += yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=1",
            "{0,false} threw ERR | n=1"),
        new(
            "CompoundAssignmentToProperty",
            "o = { n: 1 }; try { o.n += yield 0; } finally { log.push('o.n=' + o.n); }",
            "{0,false} {RV,true} {undefined,true} | o.n=1",
            "{0,false} threw ERR | o.n=1"),
        new(
            "LogicalOrAssignment",
            "var n = 0; try { n ||= yield 0; } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=0",
            "{0,false} threw ERR | n=0"),
        new(
            "AssignmentToVar",
            "var n = 1; try { n = yield 0; } finally { log.push('n=' + (n === RV ? 'RV' : n)); }",
            "{0,false} {RV,true} {undefined,true} | n=1",
            "{0,false} threw ERR | n=1"),
        new(
            "AssignmentToProperty",
            "o = { n: 1 }; try { o.n = yield 0; } finally { log.push('o.n=' + (o.n === RV ? 'RV' : o.n)); }",
            "{0,false} {RV,true} {undefined,true} | o.n=1",
            "{0,false} threw ERR | o.n=1"),
        new(
            "VariableDeclaration",
            "try { var v = yield 0; } finally { log.push('v=' + (v === RV ? 'RV' : v)); }",
            "{0,false} {RV,true} {undefined,true} | v=undefined",
            "{0,false} threw ERR | v=undefined"),
        new(
            "ArrayDestructuringOfTheYield",
            "var a = 1; try { [a] = yield 0; } finally { log.push('a=' + a); }",
            "{0,false} {RV,true} {undefined,true} | a=1",
            "{0,false} threw ERR | a=1"),
        new(
            "ArrayDestructuringDefault",
            "var a = 1, b = 1; try { [a = yield 0, b = side('b')] = []; } finally { log.push('a=' + a + ' b=' + b); }",
            "{0,false} {RV,true} {undefined,true} | a=1 b=1",
            "{0,false} threw ERR | a=1 b=1"),
        new(
            "CallArgument",
            "function f() { log.push('f called'); } try { f(yield 0, side('arg2')); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "BinaryLeftOperand",
            "try { (yield 0) + side('r'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "TemplateSubstitution",
            "try { `${yield 0}${side('t2')}`; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "YieldOperand",
            "try { yield (yield 0); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "AwaitOperand",
            "try { await (yield 0); side('after'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "AwaitAfterTheYield",
            "var n = 1; try { n += (yield 0) + await side('aw'); } finally { log.push('n=' + n); }",
            "{0,false} {RV,true} {undefined,true} | n=1",
            "{0,false} threw ERR | n=1"),
        new(
            "IfTest",
            "try { if (yield 0) side('then'); } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "WhileBodyWithoutBraces",
            "var i = 0; try { while (true) yield i++; } finally { log.push('fin i=' + i); }",
            "{0,false} {RV,true} {undefined,true} | fin i=1",
            "{0,false} threw ERR | fin i=1"),
        new(
            "SingleStatementBlockInALoop",
            "var i = 0; try { while (true) { yield i++; } } finally { log.push('fin i=' + i); }",
            "{0,false} {RV,true} {undefined,true} | fin i=1",
            "{0,false} threw ERR | fin i=1"),
        new(
            "SingleStatementTryBlock",
            "try { yield 0; } finally { log.push('fin'); }",
            "{0,false} {RV,true} {undefined,true} | fin",
            "{0,false} threw ERR | fin"),
        new(
            "DelegationInCompoundAssignment",
            "var n = 1; try { n += yield* inner(); } finally { log.push('n=' + n); }",
            "{i1,false} {RV,true} {undefined,true} | inner fin, n=1",
            "{i1,false} threw ERR | inner fin, n=1"),
        new(
            "DelegationAsCallArgument",
            "function f() { log.push('f called'); } try { f(yield* inner()); } finally { log.push('fin'); }",
            "{i1,false} {RV,true} {undefined,true} | inner fin, fin",
            "{i1,false} threw ERR | inner fin, fin"),
        new(
            "DelegationAsStatement",
            "try { yield* inner(); side('after'); } finally { log.push('fin'); }",
            "{i1,false} {RV,true} {undefined,true} | inner fin, fin",
            "{i1,false} threw ERR | inner fin, fin"),
    ];

    private static TestCases<string, string, string> Select(Row[] rows, Func<Row, string> expected)
    {
        var cases = new TestCases<string, string, string>();
        foreach (var row in rows)
        {
            cases.Add(row.Name, row.Body, expected(row));
        }

        return cases;
    }

    public static TestCases<string, string, string> GeneratorReturns() => Select(GeneratorRows, row => row.AfterReturn);

    public static TestCases<string, string, string> GeneratorThrows() => Select(GeneratorRows, row => row.AfterThrow);

    public static TestCases<string, string, string> AsyncGeneratorReturns() => Select(AsyncGeneratorRows, row => row.AfterReturn);

    public static TestCases<string, string, string> AsyncGeneratorThrows() => Select(AsyncGeneratorRows, row => row.AfterThrow);

    [TestCaseSource(nameof(GeneratorReturns))]
    public void ReturnAtAYield(string name, string body, string expected)
    {
        RunGenerator(body, "return(RV)").Should().Be(expected, name);
    }

    [TestCaseSource(nameof(GeneratorThrows))]
    public void ThrowAtAYield(string name, string body, string expected)
    {
        RunGenerator(body, "throw(ERR)").Should().Be(expected, name);
    }

    [TestCaseSource(nameof(AsyncGeneratorReturns))]
    public void ReturnAtAYieldInAnAsyncGenerator(string name, string body, string expected)
    {
        RunAsyncGenerator(body, "return(RV)").Should().Be(expected, name);
    }

    [TestCaseSource(nameof(AsyncGeneratorThrows))]
    public void ThrowAtAYieldInAnAsyncGenerator(string name, string body, string expected)
    {
        RunAsyncGenerator(body, "throw(ERR)").Should().Be(expected, name);
    }

    // ------------------------------------------------------------------ what follows the return

    private static string Drive(string generator, string steps)
    {
        return new Engine().Evaluate($$"""
            var log = [];
            function show(r) { return '{' + String(r.value) + ',' + r.done + '}'; }
            function closable(closeThrows) {
              return { [Symbol.iterator]() { var i = 0; return {
                next() { i++; log.push('next ' + i); return { value: undefined, done: false }; },
                return() { log.push('iterator closed'); if (closeThrows) throw 'close error'; return {}; } }; } };
            }
            function* inner() { yield 1; }
            function* innerCatch() { try { yield 1; } catch (e) { log.push('inner caught ' + e); } yield 2; }
            var it = ({{generator}})();
            var res = [];
            try { {{steps}}.forEach(s => res.push(show(it[s[0]](s[1])))); } catch (e) { res.push('threw ' + e); }
            res.join(' ') + ' | ' + log.join(', ');
            """).AsString();
    }

    [Test]
    public void AFinallyThatContinuesKeepsTheGeneratorRunning()
    {
        Drive(
                "function* () { var n = 1; for (var i = 0; i < 2; i++) { try { n += yield i; } finally { log.push('n=' + n); continue; } } return 'end ' + n; }",
                "[['next'], ['return', 'R'], ['next', 5], ['next']]")
            .Should().Be("{0,false} {1,false} {end 6,true} {undefined,true} | n=1, n=6");
    }

    [Test]
    public void AFinallyThatYieldsReturnsAfterItsYield()
    {
        Drive(
                "function* () { var n = 1; try { n += yield 0; } finally { var m = yield 'in finally'; log.push('n=' + n + ' m=' + m); } }",
                "[['next'], ['return', 'R'], ['next', 7], ['next']]")
            .Should().Be("{0,false} {in finally,false} {R,true} {undefined,true} | n=1 m=7");
    }

    [Test]
    public void AReturnAtAYieldInAFinallyReplacesThePendingOne()
    {
        Drive(
                "function* () { var n = 1; try { n += yield 0; } finally { n += yield 'f'; log.push('n=' + n); } }",
                "[['next'], ['return', 'R1'], ['return', 'R2'], ['next']]")
            .Should().Be("{0,false} {f,false} {R2,true} {undefined,true} | ");
    }

    [Test]
    public void AReturnInAFinallyReplacesTheRequestedOne()
    {
        Drive(
                "function* () { var n = 1; try { n += yield 0; } finally { return 'F' + n; } }",
                "[['next'], ['return', 'R'], ['next']]")
            .Should().Be("{0,false} {F1,true} {undefined,true} | ");
    }

    [Test]
    public void TheReturnRunsEveryEnclosingFinallyAndNothingBetweenThem()
    {
        Drive(
                "function* () { var n = 1; try { try { n += yield 0; } finally { log.push('inner n=' + n); } log.push('between'); } finally { log.push('outer n=' + n); } }",
                "[['next'], ['return', 'R'], ['next']]")
            .Should().Be("{0,false} {R,true} {undefined,true} | inner n=1, outer n=1");
    }

    // A yield* or a destructuring pattern can take the return() or throw() resumption without the yield it suspended
    // at ever being reached again. The resumption is still consumed: the next yield resumed by next() takes a value.

    [Test]
    public void ANextAfterAReturnTakenByADelegationIsANext()
    {
        Drive(
                "function* () { try { yield* inner(); } finally { var x = yield 'f'; log.push('x=' + x); } }",
                "[['next'], ['return', 'R'], ['next', 7], ['next']]")
            .Should().Be("{1,false} {f,false} {R,true} {undefined,true} | x=7");
    }

    [Test]
    public void ANextAfterAThrowTakenByADelegationIsANext()
    {
        Drive(
                "function* () { yield* innerCatch(); var y = yield 'after'; log.push('y=' + y); }",
                "[['next'], ['throw', 'E'], ['next'], ['next', 9], ['next']]")
            .Should().Be("{1,false} {2,false} {after,false} {undefined,true} {undefined,true} | inner caught E, y=9");
    }

    // An array pattern over an iterator closes it with the return completion instead of stepping it again.

    [Test]
    public void AReturnInsideAnIteratorPatternClosesTheIterator()
    {
        Drive(
                "function* () { var a = 1, b = 1; try { [a = yield 0, b] = closable(); } finally { log.push('a=' + a + ' b=' + b); } }",
                "[['next'], ['return', 'R'], ['next']]")
            .Should().Be("{0,false} {R,true} {undefined,true} | next 1, iterator closed, a=1 b=1");
    }

    [Test]
    public void AReturnInsideAnIteratorPatternLeavesTheEnclosingCall()
    {
        Drive(
                "function* () { var a = 1; function f() { log.push('f called'); } try { f([a = yield 0] = closable()); } finally { log.push('a=' + a); } }",
                "[['next'], ['return', 'R'], ['next']]")
            .Should().Be("{0,false} {R,true} {undefined,true} | next 1, iterator closed, a=1");
    }

    [Test]
    public void ANextAfterAReturnTakenByAnIteratorPatternIsANext()
    {
        Drive(
                "function* () { var a; try { [a = yield 0] = closable(); } finally { var m = yield 'f'; log.push('m=' + m); } }",
                "[['next'], ['return', 'R'], ['next', 7], ['next']]")
            .Should().Be("{0,false} {f,false} {R,true} {undefined,true} | next 1, iterator closed, m=7");
    }

    [Test]
    public void AFailingCloseOfAnIteratorPatternReplacesTheReturn()
    {
        // The throw from return() is what the catch clause sees, and nothing is left requesting a return after it.
        Drive(
                "function* () { var a; try { [a = yield 0] = closable(true); } catch (e) { log.push('caught ' + e); } log.push('after'); }",
                "[['next'], ['return', 'R'], ['next']]")
            .Should().Be("{0,false} {undefined,true} {undefined,true} | next 1, iterator closed, caught close error, after");
    }

    [TestCase("for (var x of closable(true)) n += yield 0;")]
    [TestCase("for (var x of closable(true)) { n += yield 0; }")]
    public void AFailingCloseOfAForOfIteratorReplacesTheReturn(string loop)
    {
        Drive(
                "function* () { var n = 1; try { " + loop + " } catch (e) { log.push('caught ' + e); } log.push('n=' + n); }",
                "[['next'], ['return', 'R'], ['next']]")
            .Should().Be("{0,false} {undefined,true} {undefined,true} | next 1, iterator closed, caught close error, n=1");
    }

    // An async generator's yield* awaits what the inner iterator answers. A rejection is the yield*'s own throw
    // completion, so it is thrown at the yield* in the body, where a try catches it, as a throw() resumption is thrown
    // at a yield; it used to reject the request and complete the generator without running the body's catch or finally.

    private static string DriveAsync(string inner, string outerBody, string steps)
    {
        return new Engine().Evaluate($$"""
            var log = [];
            function show(r) { return '{' + String(r.value) + ',' + r.done + '}'; }
            {{inner}}
            (async () => {
              var it = (async function* () { {{outerBody}} })();
              var res = [];
              try { for (var s of {{steps}}) res.push(show(await it[s[0]](s[1]))); } catch (e) { res.push('threw ' + e); }
              return res.join(' ') + ' | ' + log.join(', ');
            })();
            """).UnwrapIfPromise().AsString();
    }

    [Test]
    public void ARejectedNextOfADelegateIsThrownAtTheDelegation()
    {
        DriveAsync(
                "async function* inner() { yield 1; throw 'boom'; }",
                "try { yield* inner(); } catch (e) { log.push('caught ' + e); } yield 'after';",
                "[['next'], ['next'], ['next']]")
            .Should().Be("{1,false} {after,false} {undefined,true} | caught boom");
    }

    [Test]
    public void ARejectedReturnOfADelegateIsThrownAtTheDelegation()
    {
        DriveAsync(
                "async function* inner() { try { yield 1; } finally { throw 'from finally'; } }",
                "try { yield* inner(); } catch (e) { log.push('caught ' + e); } yield 'after';",
                "[['next'], ['return', 'R'], ['next']]")
            .Should().Be("{1,false} {after,false} {undefined,true} | caught from finally");
    }

    // ------------------------------------------------------------------ iteration protocols that call return()

    [Test]
    public void ArrayDestructuringClosesAGeneratorSuspendedInABracelessLoop()
    {
        new Engine().Evaluate("""
            var log = [];
            function* gen() { var i = 0; try { while (true) yield i++; } finally { log.push('closed at ' + i); } }
            var [a, b] = gen();
            a + ',' + b + ' | ' + log.join(', ');
            """).AsString().Should().Be("0,1 | closed at 2");
    }

    [Test]
    public void ForOfBreakClosesAGeneratorSuspendedInABracelessLoop()
    {
        new Engine().Evaluate("""
            var log = [];
            function* gen() { var i = 0; try { for (;;) yield i++; } finally { log.push('closed at ' + i); } }
            for (var x of gen()) { if (x === 2) break; }
            x + ' | ' + log.join(', ');
            """).AsString().Should().Be("2 | closed at 3");
    }
}
