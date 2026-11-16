#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the shape of a Segments object (sebastienros/jint#4452). Per
/// <a href="https://tc39.es/ecma402/#sec-%intlsegmentsprototype%-object">%IntlSegmentsPrototype%</a> a Segments object has
/// no own property: <c>containing</c> and <c>@@iterator</c> are the prototype's, an ordinary object that inherits
/// %Object.prototype%. Its <c>containing</c>
/// (<a href="https://tc39.es/ecma402/#sec-%intlsegmentsprototype%.containing">19.5.2.1</a>) and <c>@@iterator</c>
/// (<a href="https://tc39.es/ecma402/#sec-%intlsegmentsprototype%-@@iterator">19.5.2.2</a>) take the receiver check first.
/// <para>
/// Every expected value is Node 26's, for the same probe; the cross-realm ones are Node's for a <c>vm</c> context. The
/// second realm comes from <c>$262.createRealm()</c>, whose identities a ShadowRealm would hide.
/// </para>
/// </summary>
public class IntlSegmentsPrototypeTests
{
    private static string Run(string script)
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        engine.Execute("""
            const other = $262.createRealm().global;
            const seg = new Intl.Segmenter('en', { granularity: 'word' }).segment('ab cd');
            const proto = Object.getPrototypeOf(seg);
            const otherSegments = new other.Intl.Segmenter('en', { granularity: 'word' }).segment('ab cd');
            const otherProto = Object.getPrototypeOf(otherSegments);
            """);
        return engine.Evaluate(script).AsString();
    }

    /// <summary>
    /// A Segments object has no own property, and its [[Prototype]] is %IntlSegmentsPrototype%, whose own [[Prototype]] is
    /// %Object.prototype% and whose only own properties are <c>containing</c> and <c>@@iterator</c>. Until
    /// sebastienros/jint#4452 the object held both itself and inherited %Object.prototype% directly.
    /// </summary>
    [Test]
    public void ASegmentsObjectHasNoOwnPropertiesAndInheritsItsPrototype()
    {
        Run("""
            [Reflect.ownKeys(seg).length, proto === Object.prototype, Object.getPrototypeOf(proto) === Object.prototype,
                Reflect.ownKeys(proto).map(String).join(','), Object.isExtensible(proto),
                Object.prototype.toString.call(seg), Object.hasOwn(proto, 'constructor'),
                Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag) === undefined].join('|')
            """).Should().Be("0|false|true|containing,Symbol(Symbol.iterator)|true|[object Object]|false|true");
    }

    /// <summary>
    /// Both methods are writable, configurable and not enumerable, with Node's names and lengths, and neither is a
    /// constructor.
    /// </summary>
    [Test]
    public void TheMethodsHaveTheSpecifiedAttributes()
    {
        Run("""
            const out = [];
            for (const key of ['containing', Symbol.iterator]) {
                const d = Object.getOwnPropertyDescriptor(proto, key);
                const f = d.value;
                let ctor = 'ok';
                try { new f(); } catch (e) { ctor = e.constructor.name; }
                out.push([d.writable, d.enumerable, d.configurable, f.name, f.length, Object.hasOwn(f, 'prototype'), ctor].join(','));
            }
            out.join('|')
            """).Should().Be("true,false,true,containing,1,false,TypeError|true,false,true,[Symbol.iterator],0,false,TypeError");
    }

    /// <summary>
    /// The Segments object is still iterable and still answers <c>containing</c>, in every granularity, and both are now
    /// inherited: deleting the prototype's takes them away from the object.
    /// </summary>
    [Test]
    public void TheInheritedMethodsStillSegment()
    {
        Run("""
            const out = [];
            for (const granularity of ['grapheme', 'word', 'sentence']) {
                const s = new Intl.Segmenter('en', { granularity }).segment('Ab cd. Ef');
                out.push([[...s].map(x => x.segment).join('~'), s.containing(4).segment, s.containing(9), s.containing(-1)].join(','));
            }
            out.join('|')
            """).Should().Be("A~b~ ~c~d~.~ ~E~f,d,,|Ab~ ~cd~.~ ~Ef,cd,,|Ab cd. ~Ef,Ab cd. ,,");
    }

    /// <summary>
    /// Deleting the prototype's <c>containing</c> takes it away from the object, which holds none of its own.
    /// </summary>
    [Test]
    public void TheMethodsAreInheritedNotOwn()
    {
        Run("""
            const own = [Object.hasOwn(seg, 'containing'), Object.hasOwn(seg, Symbol.iterator)];
            delete proto.containing;
            own.concat(typeof seg.containing).join('|')
            """).Should().Be("false|false|undefined");
    }

    /// <summary>
    /// Step 2 of both methods is RequireInternalSlot, before any argument is coerced: a receiver that is not a Segments
    /// object (an ordinary object, an inheritor of one, the prototype itself, a Segmenter) is a TypeError and the
    /// argument's <c>valueOf</c> never runs. The error is the method's realm's.
    /// </summary>
    [Test]
    public void TheReceiverIsCheckedBeforeTheArgument()
    {
        Run("""
            const log = [];
            const arg = { valueOf() { log.push('coerced'); return 0; } };
            const receivers = { plain: {}, inheritor: Object.create(seg), proto, segmenter: new Intl.Segmenter(), undef: undefined };
            const out = [];
            for (const [name, r] of Object.entries(receivers)) {
                for (const [m, call] of [['containing', () => proto.containing.call(r, arg)], ['iterator', () => proto[Symbol.iterator].call(r)]]) {
                    try { call(); out.push(name + '.' + m + ':no throw'); } catch (e) { out.push(name + '.' + m + ':' + (e instanceof TypeError)); }
                }
            }
            let realmOfError = '?';
            try { otherProto.containing.call({}, 0); } catch (e) { realmOfError = e.constructor === other.TypeError ? 'other' : e.constructor === TypeError ? 'main' : 'neither'; }
            out.join(',') + '|' + log.length + '|' + realmOfError
            """).Should().Be("plain.containing:true,plain.iterator:true,inheritor.containing:true,inheritor.iterator:true,proto.containing:true,proto.iterator:true,segmenter.containing:true,segmenter.iterator:true,undef.containing:true,undef.iterator:true|0|other");
    }

    /// <summary>
    /// <c>containing</c> converts its argument with ToIntegerOrInfinity, so a fraction truncates, NaN and -0.5 are 0, and
    /// anything outside the string is undefined.
    /// </summary>
    [Test]
    public void ContainingConvertsItsArgument()
    {
        Run("""
            [proto.containing.call(seg, { valueOf() { return 1; } }).segment, seg.containing('x').segment, seg.containing(NaN).segment,
                seg.containing(-0.5).segment, JSON.stringify(seg.containing(2.9).segment), seg.containing(5), seg.containing(Infinity),
                seg.containing(-1), seg.containing().segment].join('|')
            """).Should().Be("ab|ab|ab|ab|\" \"||||ab");
    }

    /// <summary>
    /// The segments object's [[Prototype]] is the %IntlSegmentsPrototype% of the realm of the <c>segment</c> function that
    /// was called, whichever realm the segmenter or the caller belongs to; and it is each realm's own.
    /// </summary>
    [Test]
    public void ThePrototypeIsTheSegmentFunctionsRealms()
    {
        Run("""
            const viaOther = other.Intl.Segmenter.prototype.segment.call(new Intl.Segmenter(), 'a');
            const viaMain = Intl.Segmenter.prototype.segment.call(new other.Intl.Segmenter(), 'a');
            [otherProto !== proto, Object.getPrototypeOf(otherProto) === other.Object.prototype,
                Object.getPrototypeOf(viaOther) === otherProto, Object.getPrototypeOf(viaMain) === proto,
                Object.getPrototypeOf(new Intl.Segmenter().segment('a')) === proto,
                other.Object.prototype.toString.call(otherSegments)].join('|')
            """).Should().Be("true|true|true|true|true|[object Object]");
    }

    /// <summary>
    /// A segment data object is created in the current realm, the one of the running <c>containing</c> or of the running
    /// segment iterator <c>next</c>, not the segmenter's or the segments object's. The data object of a method borrowed
    /// across realms inherits that method's realm's %Object.prototype%.
    /// </summary>
    [Test]
    public void SegmentDataObjectsAreCreatedInTheRunningRealm()
    {
        Run("""
            const realmOf = o => Object.getPrototypeOf(o) === Object.prototype ? 'main' : Object.getPrototypeOf(o) === other.Object.prototype ? 'other' : 'neither';
            const otherIterProto = Object.getPrototypeOf(otherSegments[Symbol.iterator]());
            const mainIterProto = Object.getPrototypeOf(seg[Symbol.iterator]());
            [realmOf(seg.containing(0)), realmOf(otherSegments.containing(0)),
                realmOf(proto.containing.call(otherSegments, 0)), realmOf(otherProto.containing.call(seg, 0)),
                realmOf(seg[Symbol.iterator]().next().value), realmOf(otherSegments[Symbol.iterator]().next().value),
                realmOf(otherIterProto.next.call(seg[Symbol.iterator]()).value), realmOf(mainIterProto.next.call(otherSegments[Symbol.iterator]()).value),
                realmOf(proto[Symbol.iterator].call(otherSegments).next().value), realmOf(otherProto[Symbol.iterator].call(seg).next().value)].join('|')
            """).Should().Be("main|other|main|other|main|other|other|main|main|other");
    }

    /// <summary>
    /// A segment data object defines its properties (CreateDataPropertyOrThrow): a setter of the same name on
    /// %Object.prototype% neither runs nor blocks it. The expected value is Node 26's.
    /// </summary>
    [Test]
    public void SegmentDataObjectsDefineTheirPropertiesOutright()
    {
        Run("""
            let hits = 0;
            for (const k of ['segment', 'index', 'input', 'isWordLike']) {
                Object.defineProperty(Object.prototype, k, { set(v) { hits++; }, get() { return 'inherited'; }, configurable: true });
            }
            const viaContaining = seg.containing(0);
            const viaIterator = seg[Symbol.iterator]().next().value;
            const out = [hits, Object.keys(viaContaining).join(','), viaContaining.segment, viaIterator.index, Object.hasOwn(viaIterator, 'input')];
            for (const k of ['segment', 'index', 'input', 'isWordLike']) { delete Object.prototype[k]; }
            out.join('|')
            """).Should().Be("0|segment,index,input,isWordLike|ab|0|true");
    }

    /// <summary>
    /// Replacing one realm's method leaves the other realm's Segments objects alone.
    /// </summary>
    [Test]
    public void ReplacingOneRealmsMethodsLeavesTheOtherAlone()
    {
        Run("""
            otherProto.containing = function () { return 'replaced'; };
            [otherSegments.containing(0), seg.containing(0).segment, [...seg].length].join('|')
            """).Should().Be("replaced|ab|3");
    }

    /// <summary>
    /// A ShadowRealm has its own %IntlSegmentsPrototype%, and the principal realm's is untouched.
    /// </summary>
    [Test]
    public void AShadowRealmHasASegmentsPrototypeOfItsOwn()
    {
        new Engine().Evaluate("""
            const inner = new ShadowRealm().evaluate(`
                (function () {
                    const s = new Intl.Segmenter().segment('ab');
                    const p = Object.getPrototypeOf(s);
                    return [Reflect.ownKeys(s).length, p === Object.prototype, Reflect.ownKeys(p).length, s.containing(1).segment].join(',');
                })()
            `);
            const s = new Intl.Segmenter().segment('ab');
            inner + ' || ' + [Reflect.ownKeys(s).length, Reflect.ownKeys(Object.getPrototypeOf(s)).length].join(',');
            """).AsString().Should().Be("0,false,2,b || 0,2");
    }
}
