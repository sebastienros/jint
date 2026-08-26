using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The GlobalSymbolRegistry (https://tc39.es/ecma262/#sec-symbol.for) is a list of
/// { [[Key]], [[Symbol]] } records, so both questions asked of it -- KeyForSymbol
/// (https://tc39.es/ecma262/#sec-keyforsymbol) and CanBeHeldWeakly
/// (https://tc39.es/ecma262/#sec-canbeheldweakly) -- are about the identity of a symbol, never
/// about its description. Two symbols may share a description while only one of them is registered.
/// </summary>
public class SymbolRegistryTests
{
    [Test]
    public void KeyForAnswersForTheRegisteredSymbolOnly()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const registered = Symbol.for('moon');
            const plain = Symbol('moon');
            JSON.stringify([
                Symbol.keyFor(registered),
                Symbol.keyFor(plain) === undefined,
                Symbol.keyFor(Symbol.iterator) === undefined,
                Symbol.keyFor(Symbol.for(''))
            ]);
            """).AsString();

        result.Should().Be("""["moon",true,true,""]""");
    }

    [Test]
    public void KeyForRejectsANonSymbolAndAWrappedSymbol()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const threw = f => { try { f(); return false; } catch (e) { return e instanceof TypeError; } };
            JSON.stringify([
                threw(() => Symbol.keyFor()),
                threw(() => Symbol.keyFor(Object(Symbol('moon')))),
                Symbol.keyFor.length
            ]);
            """).AsString();

        result.Should().Be("[true,true,1]");
    }

    [Test]
    public void ASymbolSharingADescriptionWithARegisteredOneCanStillBeHeldWeakly()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Symbol.for('moon');
            const plain = Symbol('moon');
            const set = new WeakSet();
            set.add(plain);
            const map = new WeakMap();
            map.set(plain, 1);
            new WeakRef(plain);
            new FinalizationRegistry(() => { }).register(plain, 'held');
            JSON.stringify([set.has(plain), map.get(plain)]);
            """).AsString();

        result.Should().Be("[true,1]");
    }

    [Test]
    public void ARegisteredSymbolCannotBeHeldWeakly()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const registered = Symbol.for('moon');
            const threw = f => { try { f(); return false; } catch (e) { return e instanceof TypeError; } };
            JSON.stringify([
                threw(() => new WeakSet().add(registered)),
                threw(() => new WeakMap().set(registered, 1)),
                threw(() => new WeakRef(registered)),
                threw(() => new FinalizationRegistry(() => { }).register(registered, 'held'))
            ]);
            """).AsString();

        result.Should().Be("[true,true,true,true]");
    }

    [Test]
    public void SymbolForKeepsHandingBackTheSameSymbolForAKey()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            JSON.stringify([
                Symbol.for('moon') === Symbol.for('moon'),
                Symbol.for('moon') === Symbol('moon'),
                Symbol.keyFor(Symbol.for('moon'))
            ]);
            """).AsString();

        result.Should().Be("""[true,false,"moon"]""");
    }
}
